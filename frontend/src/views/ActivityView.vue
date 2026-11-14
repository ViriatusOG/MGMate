<script setup lang="ts">
import '@/assets/charges.css'
import '@/assets/statTile.css'
import '@/assets/logList.css'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ChartData, ChartOptions } from 'chart.js'
import { useVehicleStore } from '@/stores/vehicle'
import { activityApi, type ActivityTimeline } from '@/services/activityApi'
import {
  ACTIVITY_ICONS,
  activityRows,
  activitySpans,
  activityTone,
  onlineShare,
  spansTotalMs,
  timesWoken,
  type ActivitySpan,
} from '@/utils/activity'
import { formatDate, formatNumber, formatTime, numberLocale } from '@/utils/format'
import { formatDurationMs } from '@/utils/tripRows'
import { useChartTheme } from '@/composables/useChartTheme'
import { useErrorMessage } from '@/composables/useErrorMessage'
import ViewHeader from '@/components/ViewHeader.vue'
import StatTile from '@/components/StatTile.vue'
import StatsChartCard from '@/components/StatsChartCard.vue'

const { t } = useI18n()
const vehicleStore = useVehicleStore()
const chartTheme = useChartTheme()
const errorMessage = useErrorMessage()

const vin = computed(() => vehicleStore.activeVin)

function startOfDay(date: Date, offset = 0): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + offset)
}

const day = ref(startOfDay(new Date()))
const timeline = ref<ActivityTimeline | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
// Only a failed load can be tried again; a failed save says so in the same line.
const loadFailed = ref(false)

const dayEnd = computed(() => startOfDay(day.value, 1))
const isToday = computed(() => day.value.getTime() >= startOfDay(new Date()).getTime())
const dayLabel = computed(() =>
  formatDate(day.value.toISOString(), { weekday: 'long', day: 'numeric', month: 'long' }),
)

async function load() {
  await vehicleStore.fetchVehicles()
  if (!vin.value) return
  loading.value = true
  error.value = null
  loadFailed.value = false
  try {
    timeline.value = await activityApi.timeline(
      vin.value,
      day.value.toISOString(),
      dayEnd.value.toISOString(),
    )
  } catch (e) {
    error.value = errorMessage(e)
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(vin, load)

function stepDay(offset: number) {
  day.value = startOfDay(day.value, offset)
  load()
}

const rows = computed(() =>
  timeline.value ? activityRows(timeline.value.events, timeline.value.gaps) : [],
)
const online = computed(() =>
  timeline.value && (timeline.value.readings > 0 || timeline.value.gaps.length > 0)
    ? onlineShare(timeline.value.gaps, day.value, dayEnd.value)
    : null,
)
const longestGapMs = computed(() =>
  Math.max(0, ...(timeline.value?.gaps ?? []).map((g) => Date.parse(g.to) - Date.parse(g.from))),
)

// Readings per hour of the day, local time: the link to the car as it held up.
const hoursChart = computed(() => {
  const counts = Array<number>(24).fill(0)
  for (const h of timeline.value?.hours ?? []) counts[new Date(h.hour).getHours()]! += h.readings
  const { text, grid } = chartTheme.value
  const axis = { grid: { color: grid }, border: { color: grid } }
  const data: ChartData<'bar'> = {
    labels: counts.map((_, i) => String(i).padStart(2, '0')),
    datasets: [{ label: t('activity.readings'), data: counts, backgroundColor: '#3b82f6' }],
  }
  const options: ChartOptions<'bar'> = {
    locale: numberLocale(),
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { ...axis, ticks: { color: text, maxRotation: 0, autoSkip: true } },
      y: { ...axis, ticks: { color: text, precision: 0 } },
    },
  }
  return { data, options }
})

const events = computed(() => timeline.value?.events ?? [])
const driving = computed(() =>
  activitySpans(events.value, 'driveStarted', 'driveEnded', day.value, dayEnd.value),
)
const charging = computed(() =>
  activitySpans(events.value, 'chargingStarted', 'chargingStopped', day.value, dayEnd.value),
)
const quiet = computed<ActivitySpan[]>(() =>
  (timeline.value?.gaps ?? [])
    .map((g) => ({
      from: Math.max(Date.parse(g.from), day.value.getTime()),
      to: Math.min(Date.parse(g.to), dayEnd.value.getTime()),
    }))
    .filter((g) => g.to > g.from),
)
const woken = computed(() =>
  timeline.value ? timesWoken(timeline.value.gaps, day.value, dayEnd.value) : null,
)

function durationOrDash(ms: number): string {
  return timeline.value && ms > 0 ? formatDurationMs(ms, t) : '-'
}

// Where a moment or a stretch sits on the 24-hour strip, as a share of the day.
function stripPct(at: number): number {
  const start = day.value.getTime()
  return Math.max(0, Math.min(100, ((at - start) / (dayEnd.value.getTime() - start)) * 100))
}
function stripStyle(span: ActivitySpan) {
  const left = stripPct(span.from)
  return { left: `${left}%`, width: `${Math.max(stripPct(span.to) - left, 0.4)}%` }
}
const stripTicks = computed(() =>
  events.value
    .filter((e) => activityTone(e.kind) === 'access' || activityTone(e.kind) === 'climate')
    .map((e) => ({ key: `${e.kind}-${e.at}`, left: stripPct(Date.parse(e.at)), kind: e.kind })),
)
const nowPct = computed(() => (isToday.value ? stripPct(Date.now()) : null))

function gapLength(from: string, to: string): string {
  return formatDurationMs(Date.parse(to) - Date.parse(from), t)
}
</script>

<template>
  <div class="view-container charges">
    <ViewHeader :title="t('activity.title')" :subtitle="t('activity.subtitle')">
      <template #actions>
        <div class="charges__month">
          <button class="btn btn-sm" :aria-label="t('activity.previousDay')" @click="stepDay(-1)">
            <font-awesome-icon icon="chevron-left" />
          </button>
          <span class="charges__month-label">{{ dayLabel }}</span>
          <button
            class="btn btn-sm"
            :aria-label="t('activity.nextDay')"
            :disabled="isToday"
            @click="stepDay(1)"
          >
            <font-awesome-icon icon="chevron-right" />
          </button>
        </div>
      </template>
    </ViewHeader>

    <p v-if="error" class="charges__error" role="alert">
      <span>{{ error }}</span>
      <button
        v-if="loadFailed"
        type="button"
        class="btn btn-sm btn-outline-secondary"
        @click="load"
      >
        {{ t('common.retry') }}
      </button>
    </p>

    <section class="stat-tiles">
      <StatTile
        :label="t('activity.driving')"
        :value="durationOrDash(spansTotalMs(driving))"
        :note="driving.length ? t('activity.drives', driving.length) : undefined"
        highlight
      />
      <StatTile
        :label="t('activity.charging')"
        :value="durationOrDash(spansTotalMs(charging))"
        :note="charging.length ? t('activity.sessions', charging.length) : undefined"
      />
      <StatTile
        :label="t('activity.reporting')"
        :value="online !== null ? formatNumber(online * 100, 0) : '-'"
        :unit="online !== null ? '%' : undefined"
        :note="timeline ? t('activity.readingsNote', timeline.readings) : undefined"
      />
      <StatTile
        :label="t('activity.woken')"
        :value="woken !== null ? String(woken) : '-'"
        :note="timeline ? t('activity.gaps', timeline.gaps.length) : undefined"
      />
      <StatTile :label="t('activity.longestGap')" :value="durationOrDash(longestGapMs)" />
    </section>

    <section v-if="timeline" class="ll-card activity__day" :aria-label="t('activity.dayStrip')">
      <div class="activity__strip">
        <span
          v-for="(g, i) in quiet"
          :key="`q${i}`"
          class="activity__seg activity__seg--quiet"
          :style="stripStyle(g)"
        />
        <span
          v-for="(d, i) in charging"
          :key="`c${i}`"
          class="activity__seg activity__seg--charge"
          :style="stripStyle(d)"
        />
        <span
          v-for="(d, i) in driving"
          :key="`d${i}`"
          class="activity__seg activity__seg--drive"
          :style="stripStyle(d)"
        />
        <span
          v-for="tick in stripTicks"
          :key="tick.key"
          class="activity__tick"
          :style="{ left: `${tick.left}%` }"
          :title="t(`activity.kinds.${tick.kind}`)"
        />
        <span v-if="nowPct !== null" class="activity__now" :style="{ left: `${nowPct}%` }" />
      </div>
      <div class="activity__hours-axis" aria-hidden="true">
        <span v-for="h in ['00', '06', '12', '18', '24']" :key="h">{{ h }}</span>
      </div>
      <ul class="activity__legend">
        <li><i class="activity__key activity__key--drive" />{{ t('activity.driving') }}</li>
        <li><i class="activity__key activity__key--charge" />{{ t('activity.charging') }}</li>
        <li><i class="activity__key activity__key--quiet" />{{ t('activity.quiet') }}</li>
        <li><i class="activity__key activity__key--tick" />{{ t('activity.otherEvents') }}</li>
      </ul>
    </section>

    <p v-if="!loading && rows.length === 0" class="text-muted activity__empty">
      {{ t('activity.none') }}
    </p>

    <ol v-else class="ll-card activity__timeline">
      <li
        v-for="row in rows"
        :key="`${row.type}-${row.at}`"
        class="activity__row"
        :class="
          row.type === 'gap'
            ? 'activity__row--gap'
            : `activity__row--${activityTone(row.event.kind)}`
        "
      >
        <template v-if="row.type === 'event'">
          <time class="activity__time" :datetime="row.event.at">{{
            formatTime(row.event.at)
          }}</time>
          <span class="activity__icon"
            ><font-awesome-icon :icon="ACTIVITY_ICONS[row.event.kind]"
          /></span>
          <span class="activity__what">{{ t(`activity.kinds.${row.event.kind}`) }}</span>
          <span v-if="row.event.socPercent !== null" class="text-muted text-sm activity__soc"
            >{{ Math.round(row.event.socPercent) }}%</span
          >
        </template>
        <template v-else>
          <time class="activity__time" :datetime="row.gap.from">{{
            formatTime(row.gap.from)
          }}</time>
          <span class="activity__icon"><font-awesome-icon icon="moon" /></span>
          <span class="activity__what">{{
            t('activity.gap', {
              duration: gapLength(row.gap.from, row.gap.to),
              until: formatTime(row.gap.to),
            })
          }}</span>
        </template>
      </li>
    </ol>

    <details v-if="timeline && timeline.readings > 0" class="ll-card activity__hours-card">
      <summary class="card-title">
        <font-awesome-icon icon="wifi" />{{ t('activity.perHour') }}
      </summary>
      <!-- A fixed height: a chart left to size itself keeps growing as its canvas does. -->
      <div class="activity__hours ll-chart">
        <StatsChartCard
          :title="''"
          icon="wifi"
          type="bar"
          :data="hoursChart.data"
          :options="hoursChart.options"
          :show-info="false"
        />
      </div>
    </details>

    <p class="text-muted text-sm">{{ t('activity.hint') }}</p>
  </div>
</template>

<style scoped>
.activity__day {
  padding: 1rem;
  margin-bottom: 1rem;
}

.activity__strip {
  position: relative;
  height: 2rem;
  overflow: hidden;
  background: var(--color-surface-2);
  border-radius: 0.5rem;
}

.activity__seg {
  position: absolute;
  top: 0;
  bottom: 0;
}

.activity__seg--drive {
  background: #3b82f6;
}

.activity__seg--charge {
  background: #22c55e;
}

.activity__seg--quiet {
  background: repeating-linear-gradient(
    135deg,
    transparent 0 4px,
    color-mix(in srgb, var(--color-text-muted) 35%, transparent) 4px 6px
  );
}

.activity__tick {
  position: absolute;
  top: 25%;
  bottom: 25%;
  width: 2px;
  background: var(--color-text);
  border-radius: 1px;
  opacity: 0.55;
  transform: translateX(-1px);
}

.activity__now {
  position: absolute;
  top: -2px;
  bottom: -2px;
  width: 2px;
  background: var(--color-danger, #ef4444);
}

.activity__hours-axis {
  display: flex;
  justify-content: space-between;
  margin-top: 0.25rem;
  font-size: 0.75rem;
  font-variant-numeric: tabular-nums;
  color: var(--color-text-muted);
}

.activity__legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem 1rem;
  padding: 0;
  margin: 0.75rem 0 0;
  font-size: 0.8rem;
  color: var(--color-text-muted);
  list-style: none;
}

.activity__legend li {
  display: inline-flex;
  gap: 0.375rem;
  align-items: center;
}

.activity__key {
  display: inline-block;
  width: 0.75rem;
  height: 0.75rem;
  border-radius: 3px;
}

.activity__key--drive {
  background: #3b82f6;
}

.activity__key--charge {
  background: #22c55e;
}

.activity__key--quiet {
  background: repeating-linear-gradient(
    135deg,
    var(--color-surface-2) 0 3px,
    var(--color-text-muted) 3px 4px
  );
}

.activity__key--tick {
  width: 2px;
  background: var(--color-text);
  opacity: 0.55;
}

.activity__empty {
  margin-top: 1rem;
}

.activity__timeline {
  padding: 0.25rem 0;
  margin: 0 0 1rem;
  list-style: none;
}

.activity__row {
  position: relative;
  display: grid;

  --time-w: 4.75rem;

  grid-template-columns: var(--time-w) 2rem minmax(0, 1fr) auto;
  gap: 0.75rem;
  align-items: center;
  min-height: 44px;
  padding: 0.375rem 1rem;
}

/* The line that joins one event to the next, through the icons. */
.activity__row::before {
  position: absolute;
  top: 0;
  bottom: 0;
  left: calc(1rem + var(--time-w) + 0.75rem + 1rem - 1px);
  width: 2px;
  content: '';
  background: var(--color-border);
}

.activity__row:first-child::before {
  top: 50%;
}

.activity__row:last-child::before {
  bottom: 50%;
}

.activity__row--gap {
  color: var(--color-text-muted);
}

.activity__time {
  font-variant-numeric: tabular-nums;
  font-weight: 600;
  white-space: nowrap;
}

.activity__icon {
  position: relative;
  z-index: 1;
  display: grid;
  place-items: center;
  width: 2rem;
  height: 2rem;
  font-size: 0.85rem;
  color: var(--tone, var(--color-text-muted));
  background: var(--color-surface);
  border: 2px solid var(--tone, var(--color-border));
  border-radius: 50%;
}

.activity__row--drive {
  --tone: #3b82f6;
}

.activity__row--charge {
  --tone: #22c55e;
}

.activity__row--climate {
  --tone: #f59e0b;
}

.activity__row--access {
  --tone: #8b5cf6;
}

.activity__what {
  min-width: 0;
  overflow-wrap: anywhere;
}

.activity__soc {
  white-space: nowrap;
}

.activity__hours-card {
  padding: 0.75rem 1rem;
  margin-bottom: 1rem;
}

.activity__hours-card summary {
  margin: 0;
  cursor: pointer;
}

.activity__hours {
  position: relative;
  height: 14rem;
  margin-top: 0.75rem;
}

.activity__hours > * {
  height: 100%;
}
</style>
