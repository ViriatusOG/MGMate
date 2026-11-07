<script setup lang="ts">
import '@/assets/logList.css'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { useCommutePlacesStore } from '@/stores/commutePlaces'
import { tripLogApi, type TripLogEntry } from '@/services/tripLogApi'
import { chargesApi, type ChargeSession } from '@/services/chargesApi'
import { billedKwh, chargeEnergy, monthStart } from '@/utils/charges'
import { chargeCost, formatMoney } from '@/utils/tariff'
import { tripEnergy } from '@/utils/energy'
import { loggedDistanceKm } from '@/utils/tripLog'
import { formatDate, formatNumber } from '@/utils/format'
import { formatDurationMs } from '@/utils/tripRows'
import {
  isEmptyMonth,
  monthChange,
  monthReports,
  reportEfficiency,
  type MonthReport,
} from '@/utils/reports'
import { useUnits } from '@/composables/useUnits'
import { useErrorMessage } from '@/composables/useErrorMessage'
import ViewHeader from '@/components/ViewHeader.vue'

// A month at a time, set against the month before, with the year up to it in a table: what was
// driven, what it used, what was charged and what that cost.
const MONTHS = 12

const { t } = useI18n()
const vehicleStore = useVehicleStore()
const uiSettings = useUiSettingsStore()
const placesStore = useCommutePlacesStore()
const units = useUnits()
const errorMessage = useErrorMessage()

const vin = computed(() => vehicleStore.activeVin)
const capacityKwh = computed(
  () => vehicleStore.hvBatteryCapacityKwh ?? vehicleStore.currentStatus?.hvTotalCapacityKwh ?? null,
)

const thisMonth = monthStart(new Date())
// The month shown, and the last month of the year the table holds. It always opens on the month
// you are in, even on the 1st with nothing driven yet.
const selected = ref(thisMonth)
const windowEnd = ref(thisMonth)

const trips = ref<TripLogEntry[]>([])
const charges = ref<ChargeSession[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
let latestLoad = 0

async function load() {
  await vehicleStore.fetchVehicles()
  if (!vin.value) return
  if (placesStore.places.length === 0) void placesStore.fetchPlaces(vin.value)
  const load = ++latestLoad
  const from = monthStart(windowEnd.value, 1 - MONTHS).toISOString()
  const to = monthStart(windowEnd.value, 1).toISOString()
  loading.value = true
  error.value = null
  try {
    const [loadedTrips, loadedCharges] = await Promise.all([
      tripLogApi.log(vin.value, from, to),
      chargesApi.log(vin.value, from, to),
    ])
    if (load !== latestLoad) return
    trips.value = loadedTrips
    charges.value = loadedCharges
  } catch (e) {
    if (load === latestLoad) error.value = errorMessage(e)
  } finally {
    if (load === latestLoad) loading.value = false
  }
}

onMounted(load)
watch([vin, windowEnd], load)

// Picking a month keeps it in the table with the month before it, so it has something to be
// compared with; the table moves only when it has to.
function show(month: Date) {
  if (month > thisMonth) return
  selected.value = month
  const first = monthStart(windowEnd.value, 1 - MONTHS)
  if (month > windowEnd.value) windowEnd.value = month
  else if (month <= first) {
    const end = monthStart(month, MONTHS - 2)
    windowEnd.value = end > thisMonth ? thisMonth : end
  }
}

const canNext = computed(() => selected.value < thisMonth)

const reports = computed(() =>
  monthReports(windowEnd.value, MONTHS, trips.value, charges.value, {
    tripKwh: (entry) =>
      tripEnergy(
        entry,
        loggedDistanceKm(entry),
        vehicleStore.effectiveVehicleType,
        capacityKwh.value,
      )?.kwh ?? null,
    chargeKwh: (charge) => chargeEnergy(charge, capacityKwh.value)?.kwh ?? null,
    chargeCost: (charge) =>
      chargeCost(
        charge,
        billedKwh(charge, capacityKwh.value),
        uiSettings.tariff,
        placesStore.places,
      )?.amount ?? null,
  }),
)

const selectedIndex = computed(() =>
  reports.value.findIndex((m) => m.start.getTime() === selected.value.getTime()),
)
const current = computed(() => reports.value[selectedIndex.value] ?? null)
const previous = computed(() => reports.value[selectedIndex.value - 1] ?? null)

const monthTitle = computed(() => formatDate(selected.value, { month: 'long', year: 'numeric' }))
const previousName = computed(() => formatDate(monthStart(selected.value, -1), { month: 'short' }))

const currency = computed(() => uiSettings.tariff?.currency ?? 'GBP')
const priced = computed(
  () => uiSettings.tariff !== null || reports.value.some((m) => m.chargingCost > 0),
)

function money(amount: number) {
  return formatMoney(amount, currency.value)
}

/** Which way is better for a figure: less energy per km and less money are good news. */
type Better = 'up' | 'down' | null

interface Tile {
  key: string
  icon: string
  label: string
  value: string
  unit?: string
  change: number | null
  better: Better
  note?: string
}

function figure(month: MonthReport | null, pick: (m: MonthReport) => number | null) {
  return month ? pick(month) : null
}

// True when consumption reads as distance per kWh, where a bigger number is the better one.
const perDistance = computed(
  () =>
    units.value.convert('energyPerDistance', 100) > units.value.convert('energyPerDistance', 200),
)

const tiles = computed<Tile[]>(() => {
  const m = current.value
  if (!m) return []
  // An empty month is compared with nothing, rather than reading −100 % everywhere.
  const before = isEmptyMonth(m) ? null : previous.value
  const make = (
    key: string,
    icon: string,
    pick: (r: MonthReport) => number | null,
    show: (v: number) => { value: string; unit?: string },
    better: Better = null,
    note?: string,
    // What the change is worked out on, when that is not what the tile holds in metric.
    compare: (r: MonthReport) => number | null = pick,
  ): Tile => {
    const value = pick(m)
    const shown = value === null ? { value: '-' } : show(value)
    return {
      key,
      icon,
      label: t(`reports.tiles.${key}`),
      ...shown,
      change: monthChange(compare(m), figure(before, compare)),
      better,
      note,
    }
  }
  const distance = (km: number) => {
    const d = units.value.measure('distance', km, { decimals: 0, grouped: true })
    return { value: d?.value ?? '-', unit: d?.unit }
  }
  const kwh = (v: number) => ({ value: formatNumber(v, 1), unit: t('common.kwh') })
  const list: Tile[] = [
    make('distance', 'road', (r) => r.km, distance),
    make(
      'trips',
      'car',
      (r) => r.trips,
      (v) => ({ value: String(v) }),
    ),
    make(
      'drivingTime',
      'clock',
      (r) => r.drivingMs,
      (v) => ({ value: v > 0 ? formatDurationMs(v, t) : '-' }),
    ),
    make(
      'daysDriven',
      'calendar-days',
      (r) => r.daysDriven,
      (v) => ({ value: String(v) }),
    ),
    make(
      'efficiency',
      'bolt',
      reportEfficiency,
      (v) => {
        const e = units.value.measure('energyPerDistance', v)
        return { value: e?.value ?? '-', unit: e?.unit }
      },
      // The arrow follows the number shown: mi/kWh going up is good news, Wh/km going up is not.
      perDistance.value ? 'up' : 'down',
      undefined,
      (r) => {
        const e = reportEfficiency(r)
        return e === null ? null : units.value.convert('energyPerDistance', e)
      },
    ),
    // Less energy is mostly less driving, so it is neither good nor bad news.
    make('energyUsed', 'plug-circle-bolt', (r) => (r.kwhUsed > 0 ? r.kwhUsed : null), kwh),
    make(
      'charges',
      'plug',
      (r) => r.charges,
      (v) => ({ value: String(v) }),
    ),
    make('charged', 'charging-station', (r) => r.kwhCharged, kwh),
  ]
  if (priced.value) {
    list.push(
      make(
        'chargingCost',
        'coins',
        // Nothing priced is no price at all, rather than free.
        (r) => (r.chargingCost > 0 ? r.chargingCost : null),
        (v) => ({ value: money(v) }),
        'down',
        m.unpricedCharges > 0 ? t('reports.unpriced', m.unpricedCharges) : undefined,
      ),
      make(
        'costPer100',
        'tag',
        (r) => (r.km > 0 && r.chargingCost > 0 ? (r.chargingCost / r.km) * 100 : null),
        (v) => ({
          value: money(units.value.symbol('distance') === 'mi' ? v * 1.609344 : v),
          unit: `/ 100 ${units.value.symbol('distance')}`,
        }),
        'down',
      ),
    )
  }
  // The price per distance only means something once a month has a price.
  return list.filter((tile) => tile.key !== 'costPer100' || tile.value !== '-')
})

function changeText(change: number): string {
  const percent = Math.round(change * 100)
  const arrow = percent > 0 ? '▲' : percent < 0 ? '▼' : '='
  return t('reports.change', {
    change: `${arrow} ${Math.abs(percent)}%`,
    month: previousName.value,
  })
}

function changeClass(tile: Tile): string {
  if (tile.change === null || tile.better === null || Math.round(tile.change * 100) === 0) return ''
  const up = tile.change > 0
  return (tile.better === 'up') === up ? 'reports__change--good' : 'reports__change--bad'
}

// The year in the table, newest first, as the logs list.
const rows = computed(() =>
  [...reports.value].reverse().map((m) => {
    const efficiency = reportEfficiency(m)
    return {
      key: m.key,
      start: m.start,
      month: formatDate(m.start, { month: 'short', year: 'numeric' }),
      trips: m.trips,
      distance: units.value.measure('distance', m.km, { decimals: 0, grouped: true })?.value ?? '-',
      efficiency:
        efficiency !== null
          ? (units.value.measure('energyPerDistance', efficiency)?.value ?? '-')
          : '-',
      charges: m.charges,
      charged: m.kwhCharged > 0 ? formatNumber(m.kwhCharged, 0) : '-',
      cost: m.chargingCost > 0 ? money(m.chargingCost) : '-',
      selected: m.start.getTime() === selected.value.getTime(),
    }
  }),
)

const yearTotals = computed(() => {
  const all = reports.value
  const km = all.reduce((s, m) => s + m.km, 0)
  const kwhUsed = all.reduce((s, m) => s + m.kwhUsed, 0)
  const kwhUsedKm = all.reduce((s, m) => s + m.kwhUsedKm, 0)
  const efficiency = kwhUsed > 0 && kwhUsedKm > 0 ? (kwhUsed * 1000) / kwhUsedKm : null
  const charged = all.reduce((s, m) => s + m.kwhCharged, 0)
  const cost = all.reduce((s, m) => s + m.chargingCost, 0)
  return {
    trips: all.reduce((s, m) => s + m.trips, 0),
    distance: units.value.measure('distance', km, { decimals: 0, grouped: true })?.value ?? '-',
    efficiency:
      efficiency !== null
        ? (units.value.measure('energyPerDistance', efficiency)?.value ?? '-')
        : '-',
    charges: all.reduce((s, m) => s + m.charges, 0),
    charged: charged > 0 ? formatNumber(charged, 0, true) : '-',
    cost: cost > 0 ? money(cost) : '-',
  }
})
</script>

<template>
  <div class="view-container reports">
    <ViewHeader :title="t('reports.title')" :subtitle="t('reports.subtitle')" />

    <div class="reports__month">
      <button
        type="button"
        class="month-cal__nav"
        :aria-label="t('reports.previousMonth')"
        @click="show(monthStart(selected, -1))"
      >
        <font-awesome-icon icon="chevron-left" />
      </button>
      <h2 class="reports__month-title">{{ monthTitle }}</h2>
      <button
        type="button"
        class="month-cal__nav"
        :aria-label="t('reports.nextMonth')"
        :disabled="!canNext"
        @click="show(monthStart(selected, 1))"
      >
        <font-awesome-icon icon="chevron-right" />
      </button>
      <button
        v-if="canNext"
        type="button"
        class="btn btn-outline-secondary btn-sm"
        @click="show(thisMonth)"
      >
        {{ t('reports.thisMonth') }}
      </button>
    </div>

    <p v-if="error" class="text-danger" role="alert">{{ error }}</p>
    <p v-else-if="!loading && current && isEmptyMonth(current)" class="empty-state">
      {{ t('reports.emptyMonth', { month: monthTitle }) }}
    </p>

    <section class="ll-tiles reports__tiles" :aria-label="monthTitle">
      <div v-for="tile in tiles" :key="tile.key" class="ll-tile reports__tile">
        <span class="ll-tile__icon reports__icon"><font-awesome-icon :icon="tile.icon" /></span>
        <span class="ll-tile__value"
          >{{ tile.value }}<small v-if="tile.unit">{{ tile.unit }}</small></span
        >
        <span class="ll-tile__label">{{ tile.label }}</span>
        <span v-if="tile.change !== null" class="reports__change" :class="changeClass(tile)">{{
          changeText(tile.change)
        }}</span>
        <span v-if="tile.note" class="ll-tile__note">{{ tile.note }}</span>
      </div>
    </section>

    <section class="ll-card reports__year">
      <h2 class="reports__year-title">{{ t('reports.yearTitle') }}</h2>
      <div class="reports__table-wrap">
        <table class="reports__table">
          <thead>
            <tr>
              <th scope="col">{{ t('reports.table.month') }}</th>
              <th scope="col">{{ t('reports.table.trips') }}</th>
              <th scope="col">
                {{ t('reports.table.distance', { unit: units.symbol('distance') }) }}
              </th>
              <th scope="col">
                {{ t('reports.table.efficiency', { unit: units.symbol('energyPerDistance') }) }}
              </th>
              <th scope="col">{{ t('reports.table.charges') }}</th>
              <th scope="col">{{ t('reports.table.charged') }}</th>
              <th v-if="priced" scope="col">{{ t('reports.table.cost') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in rows"
              :key="row.key"
              :class="{ 'reports__row--selected': row.selected }"
              :aria-selected="row.selected"
            >
              <th scope="row">
                <button type="button" class="reports__row-button" @click="show(row.start)">
                  {{ row.month }}
                </button>
              </th>
              <td>{{ row.trips }}</td>
              <td>{{ row.distance }}</td>
              <td>{{ row.efficiency }}</td>
              <td>{{ row.charges }}</td>
              <td>{{ row.charged }}</td>
              <td v-if="priced">{{ row.cost }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr>
              <th scope="row">{{ t('reports.table.total') }}</th>
              <td>{{ yearTotals.trips }}</td>
              <td>{{ yearTotals.distance }}</td>
              <td>{{ yearTotals.efficiency }}</td>
              <td>{{ yearTotals.charges }}</td>
              <td>{{ yearTotals.charged }}</td>
              <td v-if="priced">{{ yearTotals.cost }}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </section>
  </div>
</template>

<style scoped>
.reports__month {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin-bottom: 1rem;
}

.reports__month-title {
  min-width: 10rem;
  margin: 0;
  font-size: 1.15rem;
  text-align: center;
  text-transform: capitalize;
}

.reports__icon {
  background: color-mix(in srgb, var(--color-primary) 15%, transparent);
  color: var(--color-primary);
}

.reports__change {
  margin-top: 0.15rem;
  padding: 0.1rem 0.5rem;
  border-radius: 999px;
  background: var(--color-surface-2);
  color: var(--color-text-muted);
  font-size: 0.7rem;
  font-variant-numeric: tabular-nums;
}

.reports__change--good {
  background: color-mix(in srgb, #22c55e 15%, transparent);
  color: #16a34a;
}

.reports__change--bad {
  background: color-mix(in srgb, #ef4444 15%, transparent);
  color: #dc2626;
}

.reports__year-title {
  margin: 0 0 0.75rem;
  font-size: 1rem;
}

.reports__table-wrap {
  overflow-x: auto;
}

.reports__table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.reports__table th,
.reports__table td {
  padding: 0.45rem 0.6rem;
  border-bottom: 1px solid var(--color-border);
  text-align: right;
}

.reports__table th:first-child {
  text-align: left;
}

.reports__table thead th {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 600;
}

.reports__table tfoot th,
.reports__table tfoot td {
  border-bottom: 0;
  font-weight: 700;
}

.reports__row--selected {
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
}

.reports__row-button {
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}
</style>
