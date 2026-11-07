<script setup lang="ts">
import '@/assets/charges.css'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ChartData, ChartOptions } from 'chart.js'
import { useVehicleStore } from '@/stores/vehicle'
import {
  chargesApi,
  CHARGE_NOTES_MAX_LENGTH,
  type ChargeChartPoint,
  type ChargeSession,
} from '@/services/chargesApi'
import { chargeEnergy, chargeMinutes, chargeTotals, monthStart } from '@/utils/charges'
import { formatDate, formatNumber, formatTime, numberLocale } from '@/utils/format'
import { formatDurationMs } from '@/utils/tripRows'
import { useUnits } from '@/composables/useUnits'
import { useChartTheme } from '@/composables/useChartTheme'
import { useErrorMessage } from '@/composables/useErrorMessage'
import ViewHeader from '@/components/ViewHeader.vue'
import StatsChartCard from '@/components/StatsChartCard.vue'

const { t, locale } = useI18n()
const vehicleStore = useVehicleStore()
const units = useUnits()
const chartTheme = useChartTheme()
const errorMessage = useErrorMessage()

const vin = computed(() => vehicleStore.activeVin)
const capacityKwh = computed(
  () => vehicleStore.hvBatteryCapacityKwh ?? vehicleStore.currentStatus?.hvTotalCapacityKwh ?? null,
)

const month = ref(monthStart(new Date()))
const charges = ref<ChargeSession[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const selectedId = ref<number | null>(null)
const chart = ref<ChargeChartPoint[] | null>(null)
const notesDraft = ref('')
const saving = ref(false)

const monthLabel = computed(() =>
  month.value.toLocaleDateString(locale.value, { month: 'long', year: 'numeric' }),
)
const isCurrentMonth = computed(() => month.value.getTime() >= monthStart(new Date()).getTime())

async function load() {
  await vehicleStore.fetchVehicles()
  if (!vin.value) return
  loading.value = true
  error.value = null
  try {
    charges.value = await chargesApi.log(
      vin.value,
      month.value.toISOString(),
      monthStart(month.value, 1).toISOString(),
    )
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(vin, load)

function stepMonth(offset: number) {
  month.value = monthStart(month.value, offset)
  selectedId.value = null
  load()
}

// Newest first, as the trip log lists a month.
const rows = computed(() => [...charges.value].reverse())
const totals = computed(() => chargeTotals(charges.value, capacityKwh.value))

// One bar per day of the month: the kWh added, split AC and DC.
const daily = computed(() => {
  const days = new Date(month.value.getFullYear(), month.value.getMonth() + 1, 0).getDate()
  const ac = Array<number>(days).fill(0)
  const dc = Array<number>(days).fill(0)
  for (const c of charges.value) {
    if (c.isHidden) continue
    const day = new Date(c.startedAt).getDate() - 1
    const kwh = chargeEnergy(c, capacityKwh.value)?.kwh ?? 0
    if (c.chargeType === 'DC') dc[day]! += kwh
    else ac[day]! += kwh
  }
  return { days, ac, dc }
})

const dailyChart = computed(() => {
  const { days, ac, dc } = daily.value
  const { text, grid } = chartTheme.value
  const axis = { grid: { color: grid }, border: { color: grid } }
  const data: ChartData<'bar'> = {
    labels: Array.from({ length: days }, (_, i) => String(i + 1)),
    datasets: [
      { label: t('charges.ac'), data: ac.map(round1), backgroundColor: '#3b82f6', stack: 'kwh' },
      { label: t('charges.dc'), data: dc.map(round1), backgroundColor: '#f59e0b', stack: 'kwh' },
    ],
  }
  const options: ChartOptions<'bar'> = {
    locale: numberLocale(),
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    plugins: { legend: { labels: { color: text } } },
    scales: {
      x: { ...axis, stacked: true, ticks: { color: text, maxRotation: 0, autoSkip: true } },
      y: {
        ...axis,
        stacked: true,
        ticks: { color: text },
        title: { display: true, text: t('common.kwh'), color: text },
      },
    },
  }
  return { data, options }
})

const selected = computed(() => charges.value.find((c) => c.id === selectedId.value) ?? null)

async function select(charge: ChargeSession) {
  if (selectedId.value === charge.id) {
    selectedId.value = null
    return
  }
  selectedId.value = charge.id
  notesDraft.value = charge.notes ?? ''
  chart.value = null
  if (!vin.value) return
  try {
    chart.value = await chargesApi.chart(vin.value, charge.id)
  } catch {
    chart.value = []
  }
}

const sessionChart = computed(() => {
  const points = chart.value
  if (!points || points.length < 2) return null
  const { text, grid } = chartTheme.value
  const axis = { grid: { color: grid }, border: { color: grid } }
  const data: ChartData<'line'> = {
    labels: points.map((p) => formatTime(p.recordedAt)),
    datasets: [
      {
        label: `${t('charges.power')} (kW)`,
        data: points.map((p) => p.powerKw),
        borderColor: '#3b82f6',
        backgroundColor: 'rgba(59,130,246,0.12)',
        fill: true,
        spanGaps: true,
        pointRadius: 0,
        yAxisID: 'y',
      },
      {
        label: `${t('charges.battery')} (%)`,
        data: points.map((p) => p.socPercent),
        borderColor: '#22c55e',
        spanGaps: true,
        pointRadius: 0,
        yAxisID: 'y1',
      },
    ],
  }
  const options: ChartOptions<'line'> = {
    locale: numberLocale(),
    responsive: true,
    maintainAspectRatio: true,
    aspectRatio: 2.4,
    animation: false,
    interaction: { mode: 'index', intersect: false },
    plugins: { legend: { labels: { color: text } } },
    scales: {
      x: { ...axis, ticks: { color: text, maxRotation: 0, autoSkip: true, maxTicksLimit: 6 } },
      y: { ...axis, min: 0, ticks: { color: text } },
      y1: {
        position: 'right',
        min: 0,
        max: 100,
        grid: { drawOnChartArea: false },
        ticks: { color: text },
      },
    },
  }
  return { data, options }
})

function round1(value: number): number {
  return Math.round(value * 10) / 10
}

function energyLabel(charge: ChargeSession): string {
  const energy = chargeEnergy(charge, capacityKwh.value)
  return energy ? formatNumber(energy.kwh, 1) : '-'
}

function socLabel(charge: ChargeSession): string {
  const { socStartPercent: start, socEndPercent: end } = charge
  return start !== null && end !== null ? `${Math.round(start)}% → ${Math.round(end)}%` : '-'
}

function replace(updated: ChargeSession) {
  charges.value = charges.value.map((c) => (c.id === updated.id ? updated : c))
}

async function act(action: () => Promise<void>) {
  saving.value = true
  try {
    await action()
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    saving.value = false
  }
}

function saveNotes() {
  const charge = selected.value
  if (!charge || !vin.value) return
  const notes = notesDraft.value.trim() === '' ? null : notesDraft.value.trim()
  if (notes === charge.notes) return
  const v = vin.value
  act(async () => replace(await chargesApi.setNotes(v, charge.id, notes)))
}

function toggleHidden() {
  const charge = selected.value
  if (!charge || !vin.value) return
  const v = vin.value
  act(async () => replace(await chargesApi.setHidden(v, charge.id, !charge.isHidden)))
}

function joinNext() {
  const charge = selected.value
  if (!charge || !vin.value || !window.confirm(t('charges.joinConfirm'))) return
  const v = vin.value
  act(async () => {
    await chargesApi.mergeWithNext(v, charge.id)
    await load()
  })
}

const hasNext = computed(() => {
  const charge = selected.value
  return charge !== null && charges.value.some((c) => c.startedAt > charge.startedAt)
})
</script>

<template>
  <div class="view-container charges">
    <ViewHeader :title="t('charges.title')" :subtitle="t('charges.subtitle')">
      <template #actions>
        <div class="charges__month">
          <button
            class="btn btn-sm"
            :aria-label="t('charges.previousMonth')"
            @click="stepMonth(-1)"
          >
            <font-awesome-icon icon="chevron-left" />
          </button>
          <span class="charges__month-label">{{ monthLabel }}</span>
          <button
            class="btn btn-sm"
            :aria-label="t('charges.nextMonth')"
            :disabled="isCurrentMonth"
            @click="stepMonth(1)"
          >
            <font-awesome-icon icon="chevron-right" />
          </button>
        </div>
      </template>
    </ViewHeader>

    <p v-if="error" class="charges__error" role="alert">{{ error }}</p>

    <div class="charges__tiles">
      <div class="charges__tile">
        <span class="charges__tile-label">{{ t('charges.sessions') }}</span>
        <span class="charges__tile-value">{{ totals.sessions }}</span>
        <span class="charges__tile-note">{{
          t('charges.acDcSplit', { ac: totals.ac, dc: totals.dc })
        }}</span>
      </div>
      <div class="charges__tile">
        <span class="charges__tile-label">{{ t('charges.energyAdded') }}</span>
        <span class="charges__tile-value">
          {{ formatNumber(totals.kwh, 1) }}<small>{{ t('common.kwh') }}</small>
        </span>
      </div>
      <div class="charges__tile">
        <span class="charges__tile-label">{{ t('charges.timeCharging') }}</span>
        <span class="charges__tile-value">{{ formatDurationMs(totals.minutes * 60_000, t) }}</span>
      </div>
    </div>

    <div v-if="totals.sessions > 0" class="charges__daily chart-container">
      <h2 class="card-title"><font-awesome-icon icon="chart-line" />{{ t('charges.perDay') }}</h2>
      <div class="charges__daily-canvas">
        <StatsChartCard
          class="charges__daily-chart"
          :title="''"
          icon="plug-circle-bolt"
          type="bar"
          :data="dailyChart.data"
          :options="dailyChart.options"
          :show-info="false"
        />
      </div>
    </div>

    <p v-if="!loading && rows.length === 0" class="text-muted charges__empty">
      {{ t('charges.none') }}
    </p>

    <ul class="charges__list">
      <li
        v-for="charge in rows"
        :key="charge.id"
        class="charges__item"
        :class="{ 'charges__item--hidden': charge.isHidden }"
      >
        <button
          class="charges__row"
          :aria-expanded="selectedId === charge.id"
          @click="select(charge)"
        >
          <span class="charges__when">
            <strong>{{
              formatDate(charge.startedAt, { weekday: 'short', day: 'numeric', month: 'short' })
            }}</strong>
            <span class="text-muted text-sm"
              >{{ formatTime(charge.startedAt) }} - {{ formatTime(charge.endedAt) }}</span
            >
          </span>
          <span
            class="charges__type"
            :class="`charges__type--${(charge.chargeType ?? 'AC').toLowerCase()}`"
          >
            {{ charge.chargeType ?? '?' }}
          </span>
          <span class="charges__soc">{{ socLabel(charge) }}</span>
          <span class="charges__num"
            >{{ energyLabel(charge) }} <small>{{ t('common.kwh') }}</small></span
          >
          <span class="charges__num text-muted">
            {{ charge.maxPowerKw !== null ? formatNumber(charge.maxPowerKw, 1) : '-' }}
            <small>kW</small>
          </span>
        </button>

        <div v-if="selectedId === charge.id" class="charges__detail">
          <dl class="charges__figures">
            <div>
              <dt>{{ t('charges.added') }}</dt>
              <dd>{{ energyLabel(charge) }} {{ t('common.kwh') }}</dd>
              <dd
                v-if="chargeEnergy(charge, capacityKwh)?.source === 'soc'"
                class="text-xs text-muted"
              >
                {{ t('charges.estimated') }}
              </dd>
            </div>
            <div>
              <dt>{{ t('charges.battery') }}</dt>
              <dd>{{ socLabel(charge) }}</dd>
            </div>
            <div>
              <dt>{{ t('charges.duration') }}</dt>
              <dd>{{ formatDurationMs(chargeMinutes(charge) * 60_000, t) }}</dd>
            </div>
            <div>
              <dt>{{ t('charges.peakPower') }}</dt>
              <dd>
                {{ charge.maxPowerKw !== null ? `${formatNumber(charge.maxPowerKw, 1)} kW` : '-' }}
              </dd>
            </div>
            <div v-if="charge.odometerKm !== null">
              <dt>{{ t('vehicle.odometer') }}</dt>
              <dd>
                {{ units.format('distance', charge.odometerKm, { decimals: 0, grouped: true }) }}
              </dd>
            </div>
            <div v-if="charge.stopReason">
              <dt>{{ t('charges.stopReason') }}</dt>
              <dd>{{ charge.stopReason }}</dd>
            </div>
          </dl>

          <StatsChartCard
            v-if="sessionChart"
            :title="t('charges.chart')"
            icon="chart-line"
            type="line"
            :data="sessionChart.data"
            :options="sessionChart.options"
            :show-info="false"
          />

          <label :for="`charge-notes-${charge.id}`" class="charges__notes-label">{{
            t('charges.notes')
          }}</label>
          <textarea
            :id="`charge-notes-${charge.id}`"
            v-model="notesDraft"
            class="charges__notes"
            rows="2"
            :maxlength="CHARGE_NOTES_MAX_LENGTH"
            @blur="saveNotes"
          ></textarea>

          <div class="charges__actions">
            <button
              class="btn btn-sm btn-outline-secondary"
              :disabled="saving"
              @click="toggleHidden"
            >
              <font-awesome-icon :icon="charge.isHidden ? 'eye' : 'eye-slash'" />
              {{ charge.isHidden ? t('charges.show') : t('charges.hide') }}
            </button>
            <button
              v-if="hasNext"
              class="btn btn-sm btn-outline-secondary"
              :disabled="saving"
              @click="joinNext"
            >
              <font-awesome-icon icon="link" />{{ t('charges.joinNext') }}
            </button>
          </div>
        </div>
      </li>
    </ul>
  </div>
</template>
