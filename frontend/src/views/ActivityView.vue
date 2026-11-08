<script setup lang="ts">
import '@/assets/charges.css'
import '@/assets/statTile.css'
import '@/assets/logList.css'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ChartData, ChartOptions } from 'chart.js'
import { useVehicleStore } from '@/stores/vehicle'
import { activityApi, type ActivityTimeline } from '@/services/activityApi'
import { ACTIVITY_ICONS, activityRows, onlineShare } from '@/utils/activity'
import { formatDate, formatNumber, formatTime, numberLocale } from '@/utils/format'
import { formatDurationMs } from '@/utils/tripRows'
import { useChartTheme } from '@/composables/useChartTheme'
import { useErrorMessage } from '@/composables/useErrorMessage'
import ViewHeader from '@/components/ViewHeader.vue'
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
  try {
    timeline.value = await activityApi.timeline(
      vin.value,
      day.value.toISOString(),
      dayEnd.value.toISOString(),
    )
  } catch (e) {
    error.value = errorMessage(e)
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
  timeline.value ? onlineShare(timeline.value.gaps, day.value, dayEnd.value) : null,
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

    <p v-if="error" class="charges__error" role="alert">{{ error }}</p>

    <div class="stat-tiles">
      <div class="stat-tile">
        <span class="stat-tile__label">{{ t('activity.events') }}</span>
        <span class="stat-tile__value">{{ timeline?.events.length ?? '-' }}</span>
      </div>
      <div class="stat-tile">
        <span class="stat-tile__label">{{ t('activity.reporting') }}</span>
        <span class="stat-tile__value">
          <template v-if="online !== null"
            >{{ formatNumber(online * 100, 0) }}<small>%</small></template
          >
          <template v-else>-</template>
        </span>
        <span class="stat-tile__note">{{ t('activity.gaps', timeline?.gaps.length ?? 0) }}</span>
      </div>
      <div class="stat-tile">
        <span class="stat-tile__label">{{ t('activity.longestGap') }}</span>
        <span class="stat-tile__value">{{
          longestGapMs > 0 ? formatDurationMs(longestGapMs, t) : '-'
        }}</span>
      </div>
      <div class="stat-tile">
        <span class="stat-tile__label">{{ t('activity.readings') }}</span>
        <span class="stat-tile__value">{{ timeline?.readings ?? '-' }}</span>
      </div>
    </div>

    <div v-if="timeline && timeline.readings > 0" class="chart-container">
      <h2 class="card-title"><font-awesome-icon icon="wifi" />{{ t('activity.perHour') }}</h2>
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
    </div>

    <p v-if="!loading && rows.length === 0" class="text-muted activity__empty">
      {{ t('activity.none') }}
    </p>

    <ul class="charges__list">
      <li v-for="row in rows" :key="`${row.type}-${row.at}`" class="charges__item">
        <div v-if="row.type === 'event'" class="activity__row">
          <span class="activity__time">{{ formatTime(row.event.at) }}</span>
          <font-awesome-icon :icon="ACTIVITY_ICONS[row.event.kind]" class="activity__icon" />
          <span>{{ t(`activity.kinds.${row.event.kind}`) }}</span>
          <span v-if="row.event.socPercent !== null" class="text-muted text-sm activity__soc"
            >{{ Math.round(row.event.socPercent) }}%</span
          >
        </div>
        <div v-else class="activity__row activity__row--gap">
          <span class="activity__time">{{ formatTime(row.gap.from) }}</span>
          <font-awesome-icon icon="circle-exclamation" class="activity__icon" />
          <span>{{
            t('activity.gap', {
              duration: gapLength(row.gap.from, row.gap.to),
              until: formatTime(row.gap.to),
            })
          }}</span>
        </div>
      </li>
    </ul>

    <p class="text-muted text-sm">{{ t('activity.hint') }}</p>
  </div>
</template>

<style scoped>
.activity__hours {
  position: relative;
  height: 14rem;
}

.activity__hours > * {
  height: 100%;
}

.activity__empty {
  margin-top: 1rem;
}

.activity__row {
  display: grid;
  grid-template-columns: 3.5rem 1.5rem minmax(0, 1fr) auto;
  gap: 0.5rem;
  align-items: center;
  min-height: 44px;
  padding: 0.5rem 1rem;
}

.activity__row--gap {
  color: var(--color-text-muted);
  background: var(--color-surface-2);
}

.activity__time {
  font-variant-numeric: tabular-nums;
  font-weight: 600;
}

.activity__icon {
  justify-self: center;
}
</style>
