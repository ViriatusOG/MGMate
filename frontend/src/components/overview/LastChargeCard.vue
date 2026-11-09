<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { useCommutePlacesStore } from '@/stores/commutePlaces'
import { chargesApi, type ChargeChartPoint, type ChargeSession } from '@/services/chargesApi'
import { useUnits } from '@/composables/useUnits'
import OverviewChart from './OverviewChart.vue'
import type { ChartSeries } from './chart'
import { chargeEnergy, chargeMinutes } from '@/utils/charges'
import { chargeCost, chargePlace, formatMoney } from '@/utils/tariff'
import { formatNumber, formatDateTime } from '@/utils/format'
import { formatDurationMs } from '@/utils/tripRows'

// The newest charge, as its row on the Charges page tells it, so "did it charge last night?" is
// answered without leaving the overview.
const { t } = useI18n()
const store = useVehicleStore()
const uiSettings = useUiSettingsStore()
const places = useCommutePlacesStore()
const units = useUnits()

const charge = ref<ChargeSession | null>(null)
const chart = ref<ChargeChartPoint[]>([])
const loaded = ref(false)

const capacityKwh = computed(
  () => store.hvBatteryCapacityKwh ?? store.currentStatus?.hvTotalCapacityKwh ?? null,
)

async function load() {
  const vin = store.activeVin
  if (!vin) return
  if (places.places.length === 0) void places.fetchPlaces(vin)
  try {
    const to = new Date()
    const from = new Date(to.getTime() - 60 * 86_400_000)
    const list = await chargesApi.log(vin, from.toISOString(), to.toISOString())
    charge.value =
      list
        .filter((c) => !c.isHidden)
        .sort((a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt))[0] ?? null
    void loadChart(vin)
  } catch {
    charge.value = null
  } finally {
    loaded.value = true
  }
}

// The power curve under the card: its own request, so the card shows before the curve does.
async function loadChart(vin: string) {
  const id = charge.value?.id
  chart.value = []
  if (id == null) return
  try {
    chart.value = await chargesApi.chart(vin, id)
  } catch {
    chart.value = []
  }
}

const curve = computed((): ChartSeries[] => {
  const power: [number, number][] = []
  const level: [number, number][] = []
  for (const p of chart.value) {
    const at = Date.parse(p.recordedAt)
    if (p.powerKw != null) power.push([at, Math.abs(p.powerKw)])
    if (p.socPercent != null) level.push([at, p.socPercent])
  }
  const series: ChartSeries[] = []
  if (power.length > 1)
    series.push({
      points: power,
      tone: 'warning',
      name: t('overview.chart.power'),
      unit: 'kW',
      axis: 'left',
      min: 0,
      fill: true,
      peak: (kw) => t('overview.chart.peak', { value: formatNumber(kw, 1) }),
    })
  if (level.length > 1)
    series.push({
      points: level,
      tone: 'success',
      name: t('overview.chart.soc'),
      unit: '%',
      axis: 'right',
      min: 0,
      max: 100,
      dashed: true,
    })
  return series
})

// How far the car has gone on it, by its own counters since the last charge.
const sinceThen = computed(() => {
  const x = store.currentStatus
  const km = x?.mileageSinceLastCharge ?? null
  if (km === null || !charge.value) return null
  const kwh = x?.powerUsageSinceLastCharge ?? null
  const parts = [units.value.format('distance', km, { decimals: 0 })]
  if (kwh && kwh > 0 && km > 0)
    parts.push(units.value.format('energyPerDistance', (kwh * 1000) / km))
  return parts.join(' · ')
})

const socBar = computed(() => {
  const c = charge.value
  if (!c || c.socStartPercent === null || c.socEndPercent === null) return null
  return { start: c.socStartPercent, end: c.socEndPercent }
})

onMounted(load)
watch(() => store.activeVin, load)
// A charge that just ended is the one people want to see.
watch(
  () => store.currentStatus?.isCharging,
  (now, before) => {
    if (before && !now) void load()
  },
)

// "Sat 13:41 · 14 min": the row, and the line under the chart.
const chargeWhen = computed(() => {
  const c = charge.value
  if (!c) return ''
  return `${formatDateTime(c.startedAt, { weekday: 'short', hour: '2-digit', minute: '2-digit' })} · ${formatDurationMs(chargeMinutes(c) * 60_000, t)}`
})

const rows = computed(() => {
  const c = charge.value
  if (!c) return []
  const energy = chargeEnergy(c, capacityKwh.value)
  const place = chargePlace(c, places.chargePlaces)
  const cost = chargeCost(
    c,
    c.chargerEnergyKwh ?? energy?.kwh ?? null,
    uiSettings.tariff,
    places.chargePlaces,
  )
  const soc =
    c.socStartPercent !== null && c.socEndPercent !== null
      ? `${Math.round(c.socStartPercent)}% → ${Math.round(c.socEndPercent)}%`
      : null
  const type = [c.chargeType, c.maxPowerKw !== null ? `${formatNumber(c.maxPowerKw, 1)} kW` : null]
    .filter(Boolean)
    .join(' · ')
  return [
    // The bar above says it when both ends are known.
    { key: 'battery', label: t('overview.charge.battery'), value: socBar.value ? null : soc },
    {
      key: 'when',
      label: t('overview.charge.when'),
      value: chargeWhen.value,
    },
    {
      key: 'where',
      label: t('overview.charge.where'),
      value: [t(`charges.places.${place}`), type].filter(Boolean).join(' · '),
    },
    {
      key: 'wall',
      label: t('overview.charge.fromWall'),
      value:
        c.chargerEnergyKwh != null
          ? `${formatNumber(c.chargerEnergyKwh, 1)} ${t('common.kwh')}`
          : null,
    },
    {
      key: 'cost',
      label: t('overview.charge.cost'),
      value: cost ? formatMoney(cost.amount, uiSettings.tariff?.currency ?? 'GBP') : null,
    },
    { key: 'since', label: t('overview.charge.sinceThen'), value: sinceThen.value },
  ].filter((r) => r.value)
})

const added = computed(() => {
  const c = charge.value
  const kwh = c ? chargeEnergy(c, capacityKwh.value)?.kwh : null
  return kwh == null ? null : `+${formatNumber(kwh, 1)} ${t('common.kwh')}`
})
</script>

<template>
  <section class="ov-card ov-charge" :aria-label="t('overview.charge.title')">
    <header class="ov-card__head">
      <h2 class="ov-card__title">{{ t('overview.charge.title') }}</h2>
      <RouterLink to="/charges" class="ov-card__link">{{ t('overview.charge.all') }}</RouterLink>
    </header>
    <template v-if="charge">
      <div class="ov-charge__added">
        <span class="ov-card__label">{{ t('overview.charge.added') }}</span>
        <strong>{{ added ?? '-' }}</strong>
      </div>
      <div v-if="socBar">
        <div
          class="ov-battery__bar"
          role="meter"
          aria-valuemin="0"
          aria-valuemax="100"
          :aria-valuenow="socBar.end"
          :aria-label="t('overview.charge.battery')"
        >
          <span class="ov-battery__bar-before" :style="{ width: `${socBar.start}%` }" />
          <span
            class="ov-battery__bar-now"
            :style="{
              left: `${socBar.start}%`,
              width: `${Math.max(0, socBar.end - socBar.start)}%`,
            }"
          />
        </div>
        <p class="ov-battery__ranges ov-charge__ends">
          <span>{{ Math.round(socBar.start) }}%</span>
          <span>{{ Math.round(socBar.end) }}%</span>
        </p>
      </div>
      <dl class="ov-facts">
        <div v-for="r in rows" :key="r.key">
          <dt>{{ r.label }}</dt>
          <dd>{{ r.value }}</dd>
        </div>
      </dl>
      <figure v-if="curve.length" class="ov-spark-wrap">
        <OverviewChart :series="curve" :label="t('overview.charge.curve')" />
        <figcaption class="ov-spark-cap">
          <span>{{ chargeWhen }}</span>
          <span v-if="socBar">{{ Math.round(socBar.start) }}% → {{ Math.round(socBar.end) }}%</span>
        </figcaption>
      </figure>
    </template>
    <p v-else-if="loaded" class="text-muted text-sm">{{ t('overview.charge.none') }}</p>
  </section>
</template>
