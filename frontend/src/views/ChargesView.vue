<script setup lang="ts">
import '@/assets/charges.css'
import '@/assets/logList.css'
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ChartData, ChartOptions } from 'chart.js'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { useCommutePlacesStore } from '@/stores/commutePlaces'
import { chargeCost, chargePlace, formatMoney, type ChargeCost } from '@/utils/tariff'
import TariffModal from '@/components/TariffModal.vue'
import WeekStrip from '@/components/WeekStrip.vue'
import MonthCalendar, { type CalendarDay } from '@/components/MonthCalendar.vue'
import SplitCard, { type SplitSlice } from '@/components/SplitCard.vue'
import StatTile from '@/components/StatTile.vue'
import SocBar from '@/components/SocBar.vue'
import { dayKey, groupByDay } from '@/utils/dayGroups'
import {
  chargesApi,
  CHARGE_NOTES_MAX_LENGTH,
  type ChargeChartPoint,
  type ChargeSession,
} from '@/services/chargesApi'
import {
  billedKwh,
  chargeEnergy,
  chargeLoss,
  chargeMinutes,
  chargeTotals,
  monthStart,
} from '@/utils/charges'
import { formatDate, formatNumber, formatTime, numberLocale } from '@/utils/format'
import { formatDurationMs } from '@/utils/tripRows'
import { useUnits } from '@/composables/useUnits'
import { useChartTheme } from '@/composables/useChartTheme'
import { useErrorMessage } from '@/composables/useErrorMessage'
import ViewHeader from '@/components/ViewHeader.vue'
import StatsChartCard from '@/components/StatsChartCard.vue'
import LogSearch from '@/components/LogSearch.vue'
import { matchesText, searchRange, type LogSearchQuery } from '@/utils/logSearch'
import { useMediaQuery } from '@/composables/useMediaQuery'
import ChargesLiveCard from '@/components/charges/ChargesLiveCard.vue'

const { t } = useI18n()
const vehicleStore = useVehicleStore()
const uiSettings = useUiSettingsStore()
const placesStore = useCommutePlacesStore()
const tariffOpen = ref(false)
const units = useUnits()
const chartTheme = useChartTheme()
const errorMessage = useErrorMessage()
// A phone opens on the charge under way and shows the totals as a block of four.
const isPhone = useMediaQuery('(width <= 767px)')

const vin = computed(() => vehicleStore.activeVin)
const capacityKwh = computed(
  () => vehicleStore.hvBatteryCapacityKwh ?? vehicleStore.currentStatus?.hvTotalCapacityKwh ?? null,
)

const month = ref(monthStart(new Date()))
const charges = ref<ChargeSession[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
// Only a failed load can be tried again; a failed save says so in the same line.
const loadFailed = ref(false)
const selectedId = ref<number | null>(null)
const chart = ref<ChargeChartPoint[] | null>(null)
const notesDraft = ref('')
const paidDraft = ref('')
const saving = ref(false)

// A search lists the charges over its own days that match its words, in place of the month.
const search = ref<LogSearchQuery | null>(null)

const isCurrentMonth = computed(() => month.value.getTime() >= monthStart(new Date()).getTime())

async function load() {
  await vehicleStore.fetchVehicles()
  if (!vin.value) return
  loading.value = true
  error.value = null
  loadFailed.value = false
  // Home charges are told apart by the home place; without it every charge is priced as public.
  if (placesStore.places.length === 0) placesStore.fetchPlaces(vin.value)
  // The charge under way is read from the stored status, which a page opened directly has not
  // loaded yet. It reads the database only, so it never wakes the car.
  if (!vehicleStore.currentStatus) void vehicleStore.fetchStatus(vin.value)
  const range = search.value ? searchRange(search.value) : null
  const from = range?.ok ? range.from : month.value
  const to = range?.ok ? range.to : monthStart(month.value, 1)
  try {
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

function stepMonth(offset: number) {
  search.value = null
  month.value = monthStart(month.value, offset)
  selectedId.value = null
  selectedDay.value = null
  load()
}

function jumpToToday() {
  if (isCurrentMonth.value && !search.value) return
  search.value = null
  month.value = monthStart(new Date())
  selectedId.value = null
  selectedDay.value = null
  load()
}

function searchTexts(charge: ChargeSession): (string | null)[] {
  return [
    charge.notes,
    charge.chargeType,
    charge.stopReason,
    t(`charges.places.${chargePlace(charge, placesStore.chargePlaces)}`),
  ]
}

// What the page adds up and lists: the month, or what a search found.
const listedCharges = computed(() => {
  const words = search.value?.text ?? ''
  return words ? charges.value.filter((c) => matchesText(searchTexts(c), words)) : charges.value
})

function runSearch(query: LogSearchQuery) {
  search.value = query
  selectedId.value = null
  selectedDay.value = null
  load()
}

function clearSearch() {
  search.value = null
  selectedId.value = null
  selectedDay.value = null
  load()
}

// Newest first, as the trip log lists a month.
const rows = computed(() => [...listedCharges.value].reverse())
const totals = computed(() => chargeTotals(listedCharges.value, capacityKwh.value))

const searchSummary = computed(() => {
  const query = search.value
  if (!query) return null
  const range = searchRange(query)
  const day = (d: Date) => formatDate(d, { day: 'numeric', month: 'short', year: 'numeric' })
  const priced = uiSettings.tariff !== null || spend.value.amount > 0
  return {
    found: t('charges.searchResults', totals.value.sessions),
    range: range.ok
      ? t('logSearch.range', { from: day(range.from), to: day(new Date(range.to.getTime() - 1)) })
      : '',
    figures: [
      totals.value.kwh > 0 ? `${formatNumber(totals.value.kwh, 1)} ${t('common.kwh')}` : null,
      totals.value.minutes > 0 ? formatDurationMs(totals.value.minutes * 60_000, t) : null,
      priced && spend.value.amount > 0 ? money(spend.value.amount) : null,
    ].filter((f): f is string => !!f),
  }
})

function costOf(charge: ChargeSession): ChargeCost | null {
  const kwh = billedKwh(charge, capacityKwh.value)
  return chargeCost(charge, kwh, uiSettings.tariff, placesStore.chargePlaces)
}

function money(amount: number): string {
  return formatMoney(amount, uiSettings.tariff?.currency ?? 'GBP')
}

// The month's spend over the charges whose cost is known, and the price per kWh that works out to.
const spend = computed(() => {
  let amount = 0
  let kwh = 0
  let unpriced = 0
  let home = 0
  let away = 0
  for (const c of listedCharges.value) {
    if (c.isHidden) continue
    if (chargePlace(c, placesStore.chargePlaces) === 'home') home++
    else away++
    const cost = costOf(c)
    const energy = billedKwh(c, capacityKwh.value)
    if (cost === null || energy === null) {
      unpriced++
      continue
    }
    amount += cost.amount
    kwh += energy
  }
  return { amount, perKwh: kwh > 0 ? amount / kwh : null, unpriced, home, away }
})

const selected = computed(() => charges.value.find((c) => c.id === selectedId.value) ?? null)

async function select(charge: ChargeSession) {
  if (selectedId.value === charge.id) {
    selectedId.value = null
    return
  }
  selectedId.value = charge.id
  notesDraft.value = charge.notes ?? ''
  paidDraft.value = charge.paidAmount === null ? '' : String(charge.paidAmount)
  chart.value = null
  if (!vin.value) return
  try {
    chart.value = await chargesApi.chart(vin.value, charge.id)
  } catch {
    chart.value = []
  }
}

// Power, battery and outside temperature as bands over one time axis, as the trip data chart shows a drive.
const sessionChart = computed(() => {
  const points = chart.value
  if (!points || points.length < 2) return null
  const hasTemp = points.some((p) => p.outsideTemperature != null)
  const { text, grid } = chartTheme.value
  const band = (weight: number, color: string, title: string, extra = {}) => ({
    type: 'linear' as const,
    position: 'left' as const,
    stack: 'charge',
    stackWeight: weight,
    offset: true,
    grid: { color: grid },
    border: { color: grid },
    ticks: { color, maxTicksLimit: 4 },
    title: { display: true, text: title, color, font: { size: 10 } },
    ...extra,
  })
  const scales: ChartOptions<'line'>['scales'] = {
    x: {
      grid: { color: grid },
      ticks: { color: text, maxRotation: 0, autoSkip: true, maxTicksLimit: 7 },
    },
  }
  const datasets: ChartData<'line'>['datasets'] = []
  // Chart.js stacks bands from the bottom up, so the bottom band goes first.
  if (hasTemp) {
    scales.temp = band(0.8, '#f59e0b', units.value.symbol('temperature'))
    datasets.push({
      label: t('charges.outside'),
      data: points.map((p) =>
        p.outsideTemperature == null
          ? null
          : round1(units.value.convert('temperature', p.outsideTemperature)),
      ),
      borderColor: '#f59e0b',
      yAxisID: 'temp',
    })
  }
  scales.soc = band(1, '#22c55e', '%', { min: 0, max: 100 })
  datasets.push({
    label: t('charges.battery'),
    data: points.map((p) => p.socPercent),
    borderColor: '#22c55e',
    backgroundColor: 'rgba(34,197,94,0.12)',
    fill: true,
    yAxisID: 'soc',
  })
  scales.power = band(1.4, '#3b82f6', 'kW', { min: 0 })
  datasets.push({
    label: t('charges.power'),
    data: points.map((p) => (p.powerKw === null ? null : round1(Math.abs(p.powerKw)))),
    borderColor: '#3b82f6',
    backgroundColor: 'rgba(59,130,246,0.15)',
    fill: true,
    yAxisID: 'power',
  })
  for (const d of datasets) Object.assign(d, { pointRadius: 0, spanGaps: true, tension: 0.2 })
  datasets.reverse()
  const data: ChartData<'line'> = { labels: points.map((p) => formatTime(p.recordedAt)), datasets }
  const options: ChartOptions<'line'> = {
    locale: numberLocale(),
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    interaction: { mode: 'index', intersect: false },
    plugins: { legend: { labels: { color: text, boxWidth: 10, boxHeight: 10 } } },
    scales,
  }
  return { data, options }
})

const chartFigures = computed(() => {
  const points = chart.value ?? []
  const power = points
    .map((p) => p.powerKw)
    .filter((v): v is number => v !== null)
    .map(Math.abs)
  const temps = points
    .map((p) => p.outsideTemperature)
    .filter((v): v is number => v !== null && v !== undefined)
  return {
    avgPower: power.length > 0 ? power.reduce((s, v) => s + v, 0) / power.length : null,
    tempLow: temps.length > 0 ? Math.min(...temps) : null,
    tempHigh: temps.length > 0 ? Math.max(...temps) : null,
  }
})

function temperatureRange(): string | null {
  const { tempLow, tempHigh } = chartFigures.value
  if (tempLow === null || tempHigh === null) return null
  const lo = units.value.measure('temperature', tempLow, { decimals: 0 })
  const hi = units.value.measure('temperature', tempHigh, { decimals: 0 })
  if (!lo || !hi) return null
  return lo.value === hi.value ? `${lo.value} ${lo.unit}` : `${lo.value} – ${hi.value} ${hi.unit}`
}

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

function readPaid(): number | null | undefined {
  const text = paidDraft.value.trim().replace(',', '.')
  if (text === '') return null
  const value = Number(text)
  return Number.isFinite(value) && value >= 0 ? Math.round(value * 100) / 100 : undefined
}

// Notes, place and price go together: the API replaces all three at once.
function saveEdit(place: ChargeSession['place'] = selected.value?.place ?? null) {
  const charge = selected.value
  if (!charge || !vin.value) return
  const notes = notesDraft.value.trim() === '' ? null : notesDraft.value.trim()
  const paidAmount = readPaid()
  if (paidAmount === undefined) {
    error.value = t('charges.paidInvalid')
    return
  }
  if (notes === charge.notes && place === charge.place && paidAmount === charge.paidAmount) return
  const v = vin.value
  act(async () => replace(await chargesApi.update(v, charge.id, { notes, place, paidAmount })))
}

function onPlaceChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  saveEdit(value === '' ? null : (value as ChargeSession['place']))
}

function costNote(charge: ChargeSession, cost: ChargeCost): string {
  if (cost.source === 'paid') return t('charges.costPaid')
  if (cost.source === 'work') return t('charges.costWork')
  const energy = chargeEnergy(charge, capacityKwh.value)
  const perKwh = energy && energy.kwh > 0 ? cost.amount / energy.kwh : null
  const rate = perKwh === null ? '' : formatMoney(perKwh, uiSettings.tariff!.currency, 3)
  return t(cost.source === 'home' ? 'charges.costHome' : 'charges.costPublic', { rate })
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

const visible = computed(() => listedCharges.value.filter((c) => !c.isHidden))
const days = computed(() => groupByDay(rows.value, (c) => c.startedAt))

function kwhOf(charge: ChargeSession): number | null {
  return chargeEnergy(charge, capacityKwh.value)?.kwh ?? null
}

function placeOf(charge: ChargeSession): 'home' | 'work' | 'public' {
  return chargePlace(charge, placesStore.chargePlaces)
}

function socChange(charge: ChargeSession): string | null {
  const { socStartPercent: start, socEndPercent: end } = charge
  if (start === null || end === null) return null
  const diff = Math.round(end) - Math.round(start)
  return `${Math.round(start)}% → ${Math.round(end)}% (${diff >= 0 ? '+' : ''}${diff}%)`
}

function perKwh(charge: ChargeSession): string | null {
  const cost = costOf(charge)
  const kwh = kwhOf(charge)
  if (!cost || !kwh || kwh <= 0) return null
  return formatMoney(cost.amount / kwh, uiSettings.tariff?.currency ?? 'GBP', 3)
}

function durationOf(charge: ChargeSession): string {
  return formatDurationMs(chargeMinutes(charge) * 60_000, t)
}

const peakPower = computed(() => {
  const peaks = visible.value.map((c) => c.maxPowerKw).filter((v): v is number => v !== null)
  return peaks.length > 0 ? Math.max(...peaks) : null
})

// What a day adds on average, over the days listed: the month so far, or a search's own days.
const perDayTile = computed(() => {
  const range = search.value ? searchRange(search.value) : null
  const from = range?.ok ? range.from : month.value
  const end = range?.ok ? range.to : monthStart(month.value, 1)
  const to = Math.min(end.getTime(), Date.now())
  const dayCount = Math.max(1, Math.ceil((to - from.getTime()) / 86_400_000))
  return {
    key: 'perDay',
    label: t('charges.perDayTile'),
    value: formatNumber(totals.value.kwh / dayCount, 1),
    unit: t('common.kwh'),
    note: t('charges.perDayNote', dayCount),
    askPrice: false,
  }
})

const tiles = computed(() => {
  const n = totals.value.sessions
  const priced = uiSettings.tariff !== null || spend.value.amount > 0
  const perKwh =
    spend.value.perKwh !== null
      ? `${formatMoney(spend.value.perKwh, uiSettings.tariff?.currency ?? 'GBP', 3)}/${t('common.kwh')}`
      : null
  return [
    {
      key: 'energy',
      label: t('charges.energyAdded'),
      value: formatNumber(totals.value.kwh, 1),
      unit: t('common.kwh'),
      note: t('charges.count', n),
    },
    {
      key: 'duration',
      label: t('charges.avgDuration'),
      value: n > 0 ? formatDurationMs((totals.value.minutes / n) * 60_000, t) : '-',
      unit: '',
      note:
        peakPower.value !== null
          ? t('charges.peakNote', { power: `${formatNumber(peakPower.value, 1)} kW` })
          : null,
    },
    {
      key: 'cost',
      label: t('charges.cost'),
      value: priced ? money(spend.value.amount) : '-',
      unit: '',
      // No price set: the tile offers to set one instead of showing a bare dash.
      note: perKwh,
      askPrice: !priced,
    },
    ...(isPhone.value ? [perDayTile.value] : []),
  ]
})

function splitNote(list: ChargeSession[]): string {
  const kwh = list.reduce((s, c) => s + (kwhOf(c) ?? 0), 0)
  let cost = 0
  let priced = false
  for (const c of list) {
    const x = costOf(c)
    if (x) {
      cost += x.amount
      priced = true
    }
  }
  const parts = [`${formatNumber(kwh, 1)} ${t('common.kwh')}`]
  if (priced) parts.push(money(cost))
  return parts.join(' · ')
}

const typeSlices = computed<SplitSlice[]>(() => {
  const ac = visible.value.filter((c) => c.chargeType !== 'DC')
  const dc = visible.value.filter((c) => c.chargeType === 'DC')
  return [
    { key: 'ac', label: t('charges.ac'), count: ac.length, note: splitNote(ac), color: '#3b82f6' },
    { key: 'dc', label: t('charges.dc'), count: dc.length, note: splitNote(dc), color: '#f59e0b' },
  ]
})

const PLACE_COLORS = { home: '#22c55e', work: '#8b5cf6', public: '#ef4444' } as const
const PLACE_ICONS = { home: 'house', work: 'building', public: 'charging-station' } as const

const placeSlices = computed<SplitSlice[]>(() =>
  (['home', 'work', 'public'] as const)
    .map((key) => {
      const list = visible.value.filter((c) => placeOf(c) === key)
      return {
        key,
        label: t(`charges.places.${key}`),
        count: list.length,
        note: splitNote(list),
        color: PLACE_COLORS[key],
      }
    })
    .filter((s) => s.key !== 'work' || s.count > 0),
)

function dayFigures(list: ChargeSession[]) {
  const shown = list.filter((c) => !c.isHidden)
  const kwh = shown.reduce((s, c) => s + (kwhOf(c) ?? 0), 0)
  const minutes = shown.reduce((s, c) => s + chargeMinutes(c), 0)
  let cost: number | null = null
  for (const c of shown) {
    const x = costOf(c)
    if (x) cost = (cost ?? 0) + x.amount
  }
  return {
    count: t('charges.count', shown.length),
    kwh: kwh > 0 ? `+${formatNumber(kwh, 1)} ${t('common.kwh')}` : null,
    duration: minutes > 0 ? formatDurationMs(minutes * 60_000, t) : null,
    cost: cost !== null ? money(cost) : null,
  }
}

const calendarDays = computed(() => {
  const result: Record<string, CalendarDay> = {}
  const groups = days.value.map((g) => ({
    key: g.key,
    items: g.items.filter((c) => !c.isHidden),
  }))
  const kwh = groups.map((g) => g.items.reduce((s, c) => s + (kwhOf(c) ?? 0), 0))
  const most = Math.max(1, ...kwh)
  groups.forEach((g, i) => {
    if (g.items.length === 0) return
    result[g.key] = {
      value: formatNumber(kwh[i]!, 0),
      count: g.items.length,
      level: kwh[i]! / most,
    }
  })
  return result
})

const selectedDay = ref<string | null>(null)
// A phone shows a week strip; the month calendar and the search open from it.
const pickerOpen = ref(false)

async function selectDay(key: string) {
  selectedDay.value = key
  await nextTick()
  document
    .getElementById(`charge-day-${key}`)
    ?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

const monthStats = computed(() => {
  const n = totals.value.sessions
  const list = visible.value
  const socAdded = list.reduce(
    (s, c) =>
      c.socStartPercent !== null && c.socEndPercent !== null
        ? s + Math.max(0, c.socEndPercent - c.socStartPercent)
        : s,
    0,
  )
  const daysCharged = new Set(list.map((c) => dayKey(new Date(c.startedAt)))).size
  return [
    {
      key: 'time',
      label: t('charges.timeCharging'),
      value: formatDurationMs(totals.value.minutes * 60_000, t),
    },
    {
      key: 'avg',
      label: t('charges.avgEnergy'),
      value: n > 0 ? `${formatNumber(totals.value.kwh / n, 1)} ${t('common.kwh')}` : '-',
    },
    { key: 'soc', label: t('charges.batteryAdded'), value: `${Math.round(socAdded)}%` },
    ...(totals.value.supplyKwh > 0
      ? [
          {
            key: 'supply',
            label: t('charges.fromSupply'),
            value: `${formatNumber(totals.value.supplyKwh, 1)} ${t('common.kwh')}`,
          },
          {
            key: 'loss',
            label: t('charges.loss'),
            value: `${formatNumber(
              ((totals.value.supplyKwh - totals.value.supplyBatteryKwh) / totals.value.supplyKwh) *
                100,
              1,
            )}%`,
          },
        ]
      : []),
    { key: 'days', label: t('charges.daysCharged'), value: String(daysCharged) },
    ...(spend.value.unpriced > 0 && uiSettings.tariff
      ? [{ key: 'unpriced', label: t('charges.unpriced', spend.value.unpriced), value: '' }]
      : []),
  ]
})
</script>

<template>
  <div class="view-container charges">
    <ViewHeader :title="t('charges.title')" :subtitle="t('charges.subtitle')">
      <template #actions>
        <button class="btn btn-sm btn-outline-secondary" @click="tariffOpen = true">
          <font-awesome-icon icon="coins" />{{ t('tariff.button') }}
        </button>
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

    <ChargesLiveCard v-if="isPhone" />

    <section class="stat-tiles" :aria-label="t('charges.totalsLabel')">
      <StatTile
        v-for="tile in tiles"
        :key="tile.key"
        :label="tile.label"
        :value="tile.value"
        :unit="tile.unit"
        :note="tile.note"
        :highlight="tile.key === 'energy'"
      >
        <template v-if="tile.askPrice" #note>
          <button type="button" @click="tariffOpen = true">{{ t('tariff.setUp') }}</button>
        </template>
      </StatTile>
    </section>

    <WeekStrip
      :month="month"
      :days="calendarDays"
      :selected="selectedDay"
      :open="pickerOpen"
      @select="selectDay"
      @toggle="pickerOpen = !pickerOpen"
    />

    <div class="ll-layout" :class="{ 'll-layout--picker-open': pickerOpen }">
      <aside class="ll-side">
        <LogSearch
          :active="search !== null"
          :placeholder="t('charges.searchPlaceholder')"
          @search="runSearch"
          @clear="clearSearch"
        />
        <MonthCalendar
          :month="month"
          :days="calendarDays"
          :selected="selectedDay"
          :can-next="!isCurrentMonth"
          count-icon="plug"
          :count-label="(n: number) => t('charges.count', n)"
          @prev="stepMonth(-1)"
          @next="stepMonth(1)"
          @today="jumpToToday"
          @select="selectDay"
        />
        <div v-if="totals.sessions > 0" class="ll-card charges__splits">
          <SplitCard :title="t('charges.acVsDc')" :slices="typeSlices" />
          <SplitCard :title="t('charges.homeVsPublic')" :slices="placeSlices" />
        </div>
        <div class="ll-card">
          <dl class="ll-stats">
            <div v-for="stat in monthStats" :key="stat.key">
              <dt>{{ stat.label }}</dt>
              <dd>{{ stat.value }}</dd>
            </div>
          </dl>
          <button
            v-if="!uiSettings.tariff"
            class="btn btn-sm btn-link charges__set-prices"
            @click="tariffOpen = true"
          >
            {{ t('tariff.setUp') }}
          </button>
        </div>
      </aside>

      <div class="ll-days">
        <div v-if="searchSummary" class="ll-card log-search-results" role="status">
          <font-awesome-icon icon="magnifying-glass" class="log-search-results__icon" />
          <div class="log-search-results__text">
            <strong>{{ searchSummary.found }}</strong>
            <span class="text-sm text-muted">{{ searchSummary.range }}</span>
          </div>
          <span class="ll-day__figures">
            <span v-for="figure in searchSummary.figures" :key="figure">{{ figure }}</span>
          </span>
          <button type="button" class="btn btn-outline-secondary btn-sm" @click="clearSearch">
            {{ t('logSearch.clear') }}
          </button>
        </div>
        <p v-if="!loading && rows.length === 0 && !search" class="empty-state">
          {{ t('charges.none') }}
        </p>
        <section
          v-for="day in days"
          :id="`charge-day-${day.key}`"
          :key="day.key"
          class="ll-day"
          :class="{ 'll-day--selected': selectedDay === day.key }"
        >
          <header class="ll-day__head">
            <h2 class="ll-day__title">
              {{
                formatDate(day.date, {
                  weekday: 'short',
                  day: 'numeric',
                  month: 'short',
                  year: 'numeric',
                })
              }}
            </h2>
            <span class="text-sm text-muted">{{ dayFigures(day.items).count }}</span>
            <span class="ll-day__figures">
              <strong v-if="dayFigures(day.items).kwh">{{ dayFigures(day.items).kwh }}</strong>
              <span v-if="dayFigures(day.items).duration">{{
                dayFigures(day.items).duration
              }}</span>
              <span v-if="dayFigures(day.items).cost" class="ll-cost">{{
                dayFigures(day.items).cost
              }}</span>
            </span>
          </header>

          <ul class="ll-list">
            <li
              v-for="charge in day.items"
              :key="charge.id"
              class="ll-item charges__item"
              :class="{
                'll-item--open': selectedId === charge.id,
                'll-item--hidden': charge.isHidden,
              }"
            >
              <button
                type="button"
                class="ll-row"
                :aria-expanded="selectedId === charge.id"
                @click="select(charge)"
              >
                <span
                  class="ll-row__lead charges__lead"
                  :class="`charges__lead--${(charge.chargeType ?? 'AC').toLowerCase()}`"
                >
                  <font-awesome-icon
                    :icon="charge.chargeType === 'DC' ? 'bolt' : 'plug'"
                    :title="charge.chargeType ?? undefined"
                  />
                </span>
                <span class="ll-row__main">
                  <span class="ll-row__time">
                    {{ formatTime(charge.startedAt) }} → {{ formatTime(charge.endedAt) }}
                    <small>{{ durationOf(charge) }}</small>
                  </span>
                  <span class="ll-row__meta charges__meta">
                    <span
                      class="ll-pill"
                      :class="charge.chargeType === 'DC' ? 'll-pill--warn' : 'll-pill--info'"
                      >{{ charge.chargeType ?? '?' }}</span
                    >
                    <span class="ll-pill ll-pill--muted">
                      <font-awesome-icon :icon="PLACE_ICONS[placeOf(charge)]" />
                      {{ t(`charges.places.${placeOf(charge)}`) }}
                    </span>
                    <span v-if="charge.maxPowerKw !== null"
                      >{{ t('charges.peak') }} {{ formatNumber(charge.maxPowerKw, 1) }} kW</span
                    >
                    <span v-if="charge.stopReason">· {{ charge.stopReason }}</span>
                  </span>
                </span>
                <span class="ll-row__soc">
                  <SocBar :start="charge.socStartPercent" :end="charge.socEndPercent" />
                  <span v-if="socChange(charge)">{{ socChange(charge) }}</span>
                </span>
                <span class="ll-row__figures">
                  <span class="ll-row__big"
                    >+{{ energyLabel(charge) }} <small>{{ t('common.kwh') }}</small></span
                  >
                  <span v-if="costOf(charge)" class="ll-cost">{{
                    money(costOf(charge)!.amount)
                  }}</span>
                  <span v-if="perKwh(charge)" class="ll-row__small"
                    >{{ perKwh(charge) }}/{{ t('common.kwh') }}</span
                  >
                </span>
              </button>

              <div v-if="selectedId === charge.id" class="ll-detail charges__detail">
                <div class="trip-detail__grid">
                  <div class="trip-detail__col">
                    <section class="trip-detail__card">
                      <h3 class="trip-detail__card-title">{{ t('charges.summary') }}</h3>
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
                        <div v-if="chargeLoss(charge)">
                          <dt>{{ t('charges.fromSupply') }}</dt>
                          <dd>
                            {{ formatNumber(charge.chargerEnergyKwh!, 1) }} {{ t('common.kwh') }}
                          </dd>
                          <dd class="text-xs text-muted">
                            {{
                              t('charges.lossNote', {
                                kwh: formatNumber(chargeLoss(charge)!.kwh, 1),
                                percent: formatNumber(chargeLoss(charge)!.percent, 0),
                              })
                            }}
                          </dd>
                        </div>
                        <div>
                          <dt>{{ t('charges.battery') }}</dt>
                          <dd>{{ socLabel(charge) }}</dd>
                          <dd v-if="charge.socTargetPercent !== null" class="text-xs text-muted">
                            {{ t('charges.target', { n: charge.socTargetPercent }) }}
                          </dd>
                        </div>
                        <div>
                          <dt>{{ t('charges.duration') }}</dt>
                          <dd>{{ durationOf(charge) }}</dd>
                        </div>
                        <div>
                          <dt>{{ t('charges.peakPower') }}</dt>
                          <dd>
                            {{
                              charge.maxPowerKw !== null
                                ? `${formatNumber(charge.maxPowerKw, 1)} kW`
                                : '-'
                            }}
                          </dd>
                        </div>
                        <div v-if="chartFigures.avgPower !== null">
                          <dt>{{ t('charges.avgPower') }}</dt>
                          <dd>{{ formatNumber(chartFigures.avgPower, 1) }} kW</dd>
                        </div>
                        <div v-if="temperatureRange()">
                          <dt>{{ t('charges.outside') }}</dt>
                          <dd>{{ temperatureRange() }}</dd>
                        </div>
                        <div v-if="costOf(charge)">
                          <dt>{{ t('charges.cost') }}</dt>
                          <dd>{{ money(costOf(charge)!.amount) }}</dd>
                          <dd class="text-xs text-muted">
                            {{ costNote(charge, costOf(charge)!) }}
                          </dd>
                        </div>
                        <div v-if="charge.odometerKm !== null">
                          <dt>{{ t('vehicle.odometer') }}</dt>
                          <dd>
                            {{
                              units.format('distance', charge.odometerKm, {
                                decimals: 0,
                                grouped: true,
                              })
                            }}
                          </dd>
                        </div>
                        <div v-if="charge.stopReason">
                          <dt>{{ t('charges.stopReason') }}</dt>
                          <dd>{{ charge.stopReason }}</dd>
                        </div>
                      </dl>
                    </section>

                    <section class="trip-detail__card">
                      <div class="charges__edit">
                        <label>
                          <span class="charges__notes-label">{{ t('charges.place') }}</span>
                          <select
                            class="form-select"
                            :value="charge.place ?? ''"
                            @change="onPlaceChange"
                          >
                            <option value="">
                              {{
                                t('charges.placeAuto', {
                                  place: t(
                                    `charges.places.${chargePlace({ ...charge, place: null }, placesStore.chargePlaces)}`,
                                  ),
                                })
                              }}
                            </option>
                            <option value="home">{{ t('charges.places.home') }}</option>
                            <option value="work">{{ t('charges.places.work') }}</option>
                            <option value="public">{{ t('charges.places.public') }}</option>
                          </select>
                        </label>
                        <label>
                          <span class="charges__notes-label">{{
                            t('charges.paid', { currency: uiSettings.tariff?.currency ?? 'GBP' })
                          }}</span>
                          <input
                            v-model="paidDraft"
                            class="form-control"
                            inputmode="decimal"
                            :placeholder="t('charges.paidPlaceholder')"
                            @blur="saveEdit()"
                          />
                        </label>
                      </div>
                      <label :for="`charge-notes-${charge.id}`" class="charges__notes-label">{{
                        t('charges.notes')
                      }}</label>
                      <textarea
                        :id="`charge-notes-${charge.id}`"
                        v-model="notesDraft"
                        class="charges__notes"
                        rows="2"
                        :maxlength="CHARGE_NOTES_MAX_LENGTH"
                        @blur="saveEdit()"
                      ></textarea>
                    </section>
                  </div>

                  <div class="trip-detail__col">
                    <section class="trip-detail__card">
                      <h3 class="trip-detail__card-title">{{ t('charges.chart') }}</h3>
                      <div v-if="sessionChart" class="trip-detail__chart-canvas ll-chart">
                        <StatsChartCard
                          :title="''"
                          icon="chart-line"
                          type="line"
                          :data="sessionChart.data"
                          :options="sessionChart.options"
                          :show-info="false"
                        />
                      </div>
                      <p v-else-if="chart === null" class="text-sm text-muted">
                        {{ t('common.loading') }}
                      </p>
                      <p v-else class="text-sm text-muted">{{ t('charges.noChart') }}</p>
                    </section>
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
                </div>
              </div>
            </li>
          </ul>
        </section>
      </div>
    </div>

    <TariffModal :open="tariffOpen" @close="tariffOpen = false" />
  </div>
</template>
