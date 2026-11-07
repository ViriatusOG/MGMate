<script setup lang="ts">
import '@/assets/charges.css'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ChartData, ChartOptions } from 'chart.js'
import { useVehicleStore } from '@/stores/vehicle'
import { chargesApi, type ChargeSession } from '@/services/chargesApi'
import { capacityReadings, estimateCapacity, MIN_SOC_SPAN } from '@/utils/batteryHealth'
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
// What the pack holds new: the configured size, else what the car reports.
const nominalKwh = computed(
  () => vehicleStore.hvBatteryCapacityKwh ?? vehicleStore.currentStatus?.hvTotalCapacityKwh ?? null,
)

const charges = ref<ChargeSession[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

// A year of charges: as far back as the API serves in one request.
async function load() {
  await vehicleStore.fetchVehicles()
  if (!vin.value) return
  loading.value = true
  error.value = null
  try {
    const to = new Date()
    const from = new Date(to.getTime() - 365 * 86_400_000)
    charges.value = await chargesApi.log(vin.value, from.toISOString(), to.toISOString())
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(vin, load)

const readings = computed(() => capacityReadings(charges.value, nominalKwh.value))
const estimate = computed(() => estimateCapacity(readings.value))
const health = computed(() =>
  estimate.value && nominalKwh.value ? (estimate.value.kwh / nominalKwh.value) * 100 : null,
)

const chart = computed(() => {
  const list = readings.value
  const est = estimate.value
  if (list.length === 0 || !est) return null
  const { text, grid } = chartTheme.value
  const axis = { grid: { color: grid }, border: { color: grid } }
  const data: ChartData<'line'> = {
    labels: list.map((r) => formatDate(r.at, { day: 'numeric', month: 'short' })),
    datasets: [
      {
        label: t('batteryHealth.reading'),
        data: list.map((r) => Math.round(r.kwh * 10) / 10),
        showLine: false,
        borderColor: '#60a5fa',
        backgroundColor: 'rgba(96,165,250,0.75)',
        // A charge that covered more of the battery draws bigger: it is the better sample.
        pointRadius: list.map((r) => 3 + (r.socSpan / 100) * 7),
        pointHoverRadius: list.map((r) => 4 + (r.socSpan / 100) * 7),
      },
      {
        label: t('batteryHealth.trend'),
        data: est.trend.map((v) => Math.round(v * 10) / 10),
        borderColor: '#22c55e',
        borderWidth: 2.5,
        pointRadius: 0,
      },
    ],
  }
  const options: ChartOptions<'line'> = {
    locale: numberLocale(),
    responsive: true,
    maintainAspectRatio: true,
    aspectRatio: 2.4,
    animation: false,
    plugins: { legend: { labels: { color: text } } },
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

    <p v-if="error" class="charges__error" role="alert">{{ error }}</p>

    <div class="charges__tiles">
      <div class="charges__tile">
        <span class="charges__tile-label">{{ t('batteryHealth.usableNow') }}</span>
        <span class="charges__tile-value">
          <template v-if="estimate">
            {{ formatNumber(estimate.kwh, 1)
            }}<small>± {{ formatNumber(estimate.spread, 1) }} {{ t('common.kwh') }}</small>
          </template>
          <template v-else>-</template>
        </span>
      </div>
      <div class="charges__tile">
        <span class="charges__tile-label">{{ t('batteryHealth.againstNew') }}</span>
        <span class="charges__tile-value">
          <template v-if="health !== null">{{ formatNumber(health, 0) }}<small>%</small></template>
          <template v-else>-</template>
        </span>
        <span class="charges__tile-note">{{
          nominalKwh
            ? t('batteryHealth.nominal', { kwh: formatNumber(nominalKwh, 1) })
            : t('batteryHealth.noNominal')
        }}</span>
      </div>
      <div class="charges__tile">
        <span class="charges__tile-label">{{ t('batteryHealth.basedOn') }}</span>
        <span class="charges__tile-value"
          >{{ readings.length
          }}<small>{{ t('batteryHealth.charges', readings.length) }}</small></span
        >
        <span class="charges__tile-note">{{
          t('batteryHealth.minSpan', { percent: MIN_SOC_SPAN })
        }}</span>
      </div>
      <div class="charges__tile">
        <span class="charges__tile-label">{{ t('batteryHealth.trendLabel') }}</span>
        <span class="charges__tile-value">
          <template v-if="estimate?.trendPerYear != null">
            {{ estimate.trendPerYear > 0 ? '+' : '' }}{{ formatNumber(estimate.trendPerYear, 1)
            }}<small>{{ t('batteryHealth.perYear') }}</small>
          </template>
          <template v-else>-</template>
        </span>
        <span v-if="estimate?.trendPerYear == null" class="charges__tile-note">{{
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

    <p class="text-muted text-sm">{{ t('batteryHealth.disclaimer') }}</p>
  </div>
</template>
