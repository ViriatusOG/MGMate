<script setup lang="ts">
import '@/assets/charges.css'
import '@/assets/statTile.css'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ChartData, ChartOptions } from 'chart.js'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { chargesApi, type ChargeSession } from '@/services/chargesApi'
import {
  capacityReadings,
  estimateCapacity,
  healthPercent,
  CALIBRATION_DAYS,
  MIN_SOC_SPAN,
  SETTLED_COUNT,
} from '@/utils/batteryHealth'
import { formatDate, formatNumber, numberLocale } from '@/utils/format'
import { useChartTheme } from '@/composables/useChartTheme'
import { useErrorMessage } from '@/composables/useErrorMessage'
import ViewHeader from '@/components/ViewHeader.vue'
import StatsChartCard from '@/components/StatsChartCard.vue'

const { t } = useI18n()
const vehicleStore = useVehicleStore()
const chartTheme = useChartTheme()
const errorMessage = useErrorMessage()

const vin = computed(() => vehicleStore.activeVin)
const uiSettings = useUiSettingsStore()

// What the pack holds new: the size the deployment sets, else the one picked on this page, else
// what the car reports.
const nominalKwh = computed(
  () =>
    vehicleStore.hvBatteryCapacityKwh ??
    uiSettings.batteryPackKwh ??
    vehicleStore.currentStatus?.hvTotalCapacityKwh ??
    null,
)

// The MG4's packs as MG sells them (51 kWh LFP, 64 and 77 kWh), to pick from when the deployment does not set one.
const PACK_SIZES = [51, 64, 77] as const
const canPickPack = computed(() => vehicleStore.hvBatteryCapacityKwh === null)

function pickPack(kwh: number) {
  uiSettings.batteryPackKwh = uiSettings.batteryPackKwh === kwh ? null : kwh
}

// The health gauge: three quarters of a circle, filled to the share of new capacity left.
const GAUGE_R = 46
const gaugeLength = 2 * Math.PI * GAUGE_R * 0.75
const gaugeFill = computed(() =>
  health.value === null ? 0 : (Math.min(100, Math.max(0, health.value)) / 100) * gaugeLength,
)

// While calibrating: short dashes along the same three quarters, not round the whole circle.
const calibratingDashes = `${'4 14 '.repeat(Math.floor(gaugeLength / 18))}4 1000`

const charges = ref<ChargeSession[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
// Only a failed load can be tried again; a failed save says so in the same line.
const loadFailed = ref(false)

// A year of charges: as far back as the API serves in one request.
async function load() {
  await vehicleStore.fetchVehicles()
  if (!vin.value) return
  loading.value = true
  error.value = null
  loadFailed.value = false
  try {
    const to = new Date()
    const from = new Date(to.getTime() - 365 * 86_400_000)
    charges.value = await chargesApi.log(vin.value, from.toISOString(), to.toISOString())
  } catch (e) {
    error.value = errorMessage(e)
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(vin, load)

const readings = computed(() => capacityReadings(charges.value, nominalKwh.value))
const estimate = computed(() => estimateCapacity(readings.value))
const health = computed(() => healthPercent(estimate.value, nominalKwh.value))
// Readings there are, but none taken soon enough after a full charge to trust.
const calibrating = computed(() => estimate.value !== null && !estimate.value.calibrated)
// The charges the estimate is made of: the calibrated ones once there are any.
const basedOn = computed(() => estimate.value?.count ?? 0)
const early = computed(() => !!estimate.value?.calibrated && estimate.value.count < SETTLED_COUNT)
const aboveNew = computed(
  () => !!estimate.value?.calibrated && !!nominalKwh.value && estimate.value.kwh > nominalKwh.value,
)

const chart = computed(() => {
  const list = readings.value
  const est = estimate.value
  if (list.length === 0 || !est) return null
  // The fitted line and its margin over the readings it was made of; the others get gaps.
  let k = 0
  const trendAt = list.map((r) => (!est.calibrated || r.calibrated ? est.trend[k++]! : null))
  const round = (v: number | null) => (v === null ? null : Math.round(v * 10) / 10)
  const point = (r: (typeof list)[number]) => 3 + (r.socSpan / 100) * 7
  const { text, grid } = chartTheme.value
  const axis = { grid: { color: grid }, border: { color: grid } }
  const data: ChartData<'line'> = {
    labels: list.map((r) => formatDate(r.at, { day: 'numeric', month: 'short' })),
    datasets: [
      {
        label: t('batteryHealth.reading'),
        data: list.map((r) => (r.calibrated ? round(r.kwh) : null)),
        showLine: false,
        borderColor: '#60a5fa',
        backgroundColor: 'rgba(96,165,250,0.75)',
        // A charge that covered more of the battery draws bigger: it is the better sample.
        pointRadius: list.map(point),
        pointHoverRadius: list.map((r) => point(r) + 1),
      },
      // Readings from before a full charge: hollow, and out of the estimate once any are calibrated.
      {
        label: t('batteryHealth.uncalibratedReading'),
        data: list.map((r) => (r.calibrated ? null : round(r.kwh))),
        showLine: false,
        borderColor: '#8892a4',
        backgroundColor: 'transparent',
        pointBorderWidth: 1.5,
        pointRadius: list.map(point),
        pointHoverRadius: list.map((r) => point(r) + 1),
      },
      ...(est.calibrated
        ? [
            {
              label: t('batteryHealth.trend'),
              data: trendAt.map(round),
              borderColor: '#22c55e',
              borderWidth: 2.5,
              pointRadius: 0,
              spanGaps: true,
            },
            // The margin around the trend, shaded between its two edges.
            {
              label: t('batteryHealth.margin'),
              data: trendAt.map((v) => round(v === null ? null : v + est.spread)),
              borderColor: 'transparent',
              backgroundColor: 'rgba(34,197,94,0.12)',
              pointRadius: 0,
              spanGaps: true,
              fill: '+1',
            },
            {
              label: '',
              data: trendAt.map((v) => round(v === null ? null : v - est.spread)),
              borderColor: 'transparent',
              pointRadius: 0,
              spanGaps: true,
            },
          ]
        : []),
      ...(nominalKwh.value
        ? [
            {
              label: t('batteryHealth.newLine', { kwh: formatNumber(nominalKwh.value, 0) }),
              data: list.map(() => nominalKwh.value),
              borderColor: '#8892a4',
              borderDash: [5, 5],
              borderWidth: 1.5,
              pointRadius: 0,
            },
          ]
        : []),
    ],
  }
  const options: ChartOptions<'line'> = {
    locale: numberLocale(),
    responsive: true,
    maintainAspectRatio: true,
    aspectRatio: 2.4,
    animation: false,
    plugins: {
      legend: {
        labels: {
          color: text,
          // No entry for the margin's lower edge, nor for a kind of reading there is none of.
          filter: (item, chartData) =>
            item.text !== '' &&
            (chartData.datasets[item.datasetIndex ?? 0]?.data ?? []).some((v) => v !== null),
        },
      },
    },
    scales: {
      x: { ...axis, ticks: { color: text, maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } },
      y: {
        ...axis,
        ticks: { color: text },
        title: { display: true, text: t('common.kwh'), color: text },
      },
    },
  }
  return { data, options }
})
</script>

<template>
  <div class="view-container charges">
    <ViewHeader :title="t('batteryHealth.title')" :subtitle="t('batteryHealth.subtitle')" />

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

    <section class="bh-hero">
      <svg
        class="bh-gauge"
        viewBox="0 0 120 110"
        role="img"
        :aria-label="t('batteryHealth.againstNew')"
      >
        <circle
          cx="60"
          cy="60"
          :r="GAUGE_R"
          class="bh-gauge__track"
          :stroke-dasharray="`${gaugeLength} 1000`"
        />
        <circle
          v-if="calibrating"
          cx="60"
          cy="60"
          :r="GAUGE_R"
          class="bh-gauge__calibrating"
          :stroke-dasharray="calibratingDashes"
        />
        <circle
          v-else-if="health !== null"
          cx="60"
          cy="60"
          :r="GAUGE_R"
          class="bh-gauge__fill"
          :stroke-dasharray="`${gaugeFill} 1000`"
        />
        <text x="60" y="64" class="bh-gauge__value">
          {{ health !== null ? `${formatNumber(health, 0)}%` : '-' }}
        </text>
        <text x="60" y="82" class="bh-gauge__label">
          {{ calibrating ? t('batteryHealth.calibrating') : t('batteryHealth.health') }}
        </text>
      </svg>
      <div class="bh-hero__text">
        <span class="stat-tile__label">{{ t('batteryHealth.usableNow') }}</span>
        <span class="bh-hero__kwh">
          <template v-if="estimate?.calibrated">
            {{ formatNumber(estimate.kwh, 1)
            }}<small>± {{ formatNumber(estimate.spread, 1) }} {{ t('common.kwh') }}</small>
          </template>
          <template v-else>-</template>
        </span>
        <span class="text-muted text-sm"
          >{{
            nominalKwh
              ? t('batteryHealth.nominal', { kwh: formatNumber(nominalKwh, 1) })
              : t('batteryHealth.noNominal')
          }}<template v-if="calibrating && estimate">
            ·
            {{
              t(
                'batteryHealth.firstReadings',
                { kwh: formatNumber(estimate.kwh, 0), count: estimate.count },
                estimate.count,
              )
            }}</template
          ></span
        >
        <div v-if="canPickPack" class="bh-pack" role="group" :aria-label="t('batteryHealth.pack')">
          <span class="text-muted text-sm">{{ t('batteryHealth.pack') }}</span>
          <button
            v-for="kwh in PACK_SIZES"
            :key="kwh"
            type="button"
            class="bh-pack__chip"
            :aria-pressed="uiSettings.batteryPackKwh === kwh"
            @click="pickPack(kwh)"
          >
            {{ kwh }} {{ t('common.kwh') }}
          </button>
        </div>
        <div v-if="calibrating" class="bh-note bh-note--warn" role="status">
          <font-awesome-icon icon="triangle-exclamation" class="bh-note__icon" />
          <div>
            <strong>{{ t('batteryHealth.calibrateTitle') }}</strong>
            {{ t('batteryHealth.calibrateText', { days: CALIBRATION_DAYS }) }}
          </div>
        </div>
        <div v-if="aboveNew && nominalKwh" class="bh-note" role="status">
          <font-awesome-icon icon="circle-info" class="bh-note__icon" />
          <div>
            <strong>{{ t('batteryHealth.aboveNewTitle') }}</strong>
            {{ t('batteryHealth.aboveNewText', { kwh: formatNumber(nominalKwh, 1) }) }}
          </div>
        </div>
        <div v-if="early && estimate" class="bh-note" role="status">
          <font-awesome-icon icon="circle-info" class="bh-note__icon" />
          <div>
            <strong>{{
              t('batteryHealth.earlyTitle', { count: estimate.count }, estimate.count)
            }}</strong>
            {{ t('batteryHealth.earlyText', { count: SETTLED_COUNT, percent: MIN_SOC_SPAN }) }}
          </div>
        </div>
      </div>
    </section>

    <div class="stat-tiles">
      <div class="stat-tile">
        <span class="stat-tile__label">{{ t('batteryHealth.basedOn') }}</span>
        <span class="stat-tile__value"
          >{{ basedOn }}<small>{{ t('batteryHealth.charges', basedOn) }}</small></span
        >
        <span class="stat-tile__note">{{
          t('batteryHealth.minSpan', { percent: MIN_SOC_SPAN })
        }}</span>
      </div>
      <div class="stat-tile">
        <span class="stat-tile__label">{{ t('batteryHealth.trendLabel') }}</span>
        <span class="stat-tile__value">
          <template v-if="estimate?.trendPerYear != null">
            {{ estimate.trendPerYear > 0 ? '+' : '' }}{{ formatNumber(estimate.trendPerYear, 1)
            }}<small>{{ t('batteryHealth.perYear') }}</small>
          </template>
          <template v-else>-</template>
        </span>
        <span v-if="estimate?.trendPerYear == null" class="stat-tile__note">{{
          t('batteryHealth.trendPending')
        }}</span>
      </div>
    </div>

    <StatsChartCard
      v-if="chart"
      :title="t('batteryHealth.chart')"
      icon="chart-line"
      type="line"
      :data="chart.data"
      :options="chart.options"
      :show-info="false"
    />

    <p v-if="!loading && readings.length === 0" class="text-muted charges__empty">
      {{ t('batteryHealth.none', { percent: MIN_SOC_SPAN }) }}
    </p>

    <p class="text-muted text-sm bh-disclaimer">{{ t('batteryHealth.disclaimer') }}</p>
  </div>
</template>

<style scoped>
.bh-hero {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 1.5rem;
  margin-bottom: 1rem;
  padding: 1.1rem 1.25rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
  background: var(--color-surface);
}

.bh-gauge {
  width: 9rem;
  flex-shrink: 0;
}

.bh-gauge circle {
  fill: none;
  stroke-width: 10;
  stroke-linecap: round;
  transform: rotate(135deg);
  transform-origin: 60px 60px;
}

.bh-gauge__track {
  stroke: var(--color-surface-2);
}

.bh-gauge__fill {
  stroke: var(--color-success);
}

.bh-gauge__calibrating {
  stroke: var(--color-warning);
  stroke-linecap: butt !important;
  opacity: 0.6;
}

.bh-gauge__value {
  fill: var(--color-text);
  font-size: 22px;
  font-weight: 800;
  text-anchor: middle;
}

.bh-gauge__label {
  fill: var(--color-text-muted);
  font-size: 9px;
  letter-spacing: 0.08em;
  text-anchor: middle;
  text-transform: uppercase;
}

.bh-hero__text {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 0.3rem;
  min-width: 14rem;
}

.bh-hero__kwh {
  font-size: 2rem;
  font-weight: 800;
  font-variant-numeric: tabular-nums;
}

.bh-hero__kwh small {
  margin-left: 0.3em;
  font-size: 0.95rem;
  font-weight: 500;
  color: var(--color-text-muted);
}

.bh-pack {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
  margin-top: 0.4rem;
}

.bh-pack__chip {
  padding: 0.2rem 0.65rem;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: none;
  color: var(--color-text-muted);
  font-size: 0.82rem;
  cursor: pointer;
}

.bh-pack__chip[aria-pressed='true'] {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 18%, transparent);
  color: var(--color-text);
}

@media (width <= 767px) {
  .bh-hero {
    justify-content: center;
    text-align: center;
  }

  .bh-pack {
    justify-content: center;
  }
}

.bh-note {
  display: flex;
  gap: 0.6rem;
  margin-top: 0.6rem;
  padding: 0.65rem 0.85rem;
  border: 1px solid color-mix(in srgb, var(--color-primary) 45%, transparent);
  border-radius: var(--radius);
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  font-size: 0.9rem;
  text-align: left;
}

.bh-note--warn {
  border-color: color-mix(in srgb, var(--color-warning) 45%, transparent);
  background: color-mix(in srgb, var(--color-warning) 10%, transparent);
}

.bh-note strong {
  display: block;
  margin-bottom: 0.1rem;
}

.bh-note__icon {
  flex-shrink: 0;
  margin-top: 0.2rem;
}

.bh-note--warn .bh-note__icon {
  color: var(--color-warning);
}

.bh-disclaimer {
  margin-top: 0.75rem;
}
</style>
