<script setup lang="ts">
import '@/assets/tripLog.css'
import '@/assets/logList.css'
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useTripLogStore } from '@/stores/tripLog'
import { useUiSettingsStore } from '@/stores/settingsUi'
import TripLogItem from '@/components/TripLogItem.vue'
import TripLogDetail from '@/components/TripLogDetail.vue'
import CommutePlacesModal from '@/components/CommutePlacesModal.vue'
import DetailModal from '@/components/DetailModal.vue'
import { useCommutePlacesStore } from '@/stores/commutePlaces'
import { TRIP_PURPOSES, type TripPurpose } from '@/services/tripLogApi'
import { downloadCsv } from '@/utils/download'
import { formatDate, formatNumber } from '@/utils/format'
import {
  distanceShare,
  PURPOSE_CARD_VARIANTS,
  PURPOSE_ICONS,
  PURPOSE_KEYS,
  type TripLogPeriod,
} from '@/utils/tripLog'
import { tripLogCsv, tripLogFileName } from '@/utils/tripLogCsv'
import { formatDurationMs } from '@/utils/tripRows'
import type { Measure } from '@/utils/units'
import { useUnits } from '@/composables/useUnits'
import { useErrorMessage } from '@/composables/useErrorMessage'
import { useTripRoute } from '@/composables/useTripRoute'
import ViewHeader from '@/components/ViewHeader.vue'
import StatTile from '@/components/StatTile.vue'
import WeekStrip from '@/components/WeekStrip.vue'
import MonthCalendar, { type CalendarDay } from '@/components/MonthCalendar.vue'
import { groupByDay } from '@/utils/dayGroups'
import { loggedDistanceKm } from '@/utils/tripLog'
import { tripEnergy } from '@/utils/energy'
import { cheapestHomePrice, formatMoney } from '@/utils/tariff'
import LogSearch from '@/components/LogSearch.vue'
import { matchesText, searchRange, type LogSearchQuery } from '@/utils/logSearch'
import { cityName, postalAddressLabel } from '@/utils/places'
import { commutePlaceAt } from '@/utils/commutePlaces'
import type { TripLogEntry } from '@/services/tripLogApi'

const { t } = useI18n()
const vehicleStore = useVehicleStore()
const store = useTripLogStore()
const commutePlaces = useCommutePlacesStore()
const uiSettings = useUiSettingsStore()
const units = useUnits()
const errorMessage = useErrorMessage()

const vin = computed(() => vehicleStore.activeVin)

// The current month to start with: the one a driver is most likely filling in.
const today = new Date()
const shownMonth = ref(new Date(today.getFullYear(), today.getMonth(), 1))
// The whole year at once, for the tax log's yearly export; the calendar still pages by month.
const wholeYear = ref(false)
const period = computed<TripLogPeriod>(() => ({
  year: shownMonth.value.getFullYear(),
  month: wholeYear.value ? null : shownMonth.value.getMonth(),
}))
const canNextMonth = computed(
  () => shownMonth.value < new Date(today.getFullYear(), today.getMonth(), 1),
)

function stepMonth(offset: number) {
  search.value = null
  shownMonth.value = new Date(
    shownMonth.value.getFullYear(),
    shownMonth.value.getMonth() + offset,
    1,
  )
}

function jumpToToday() {
  search.value = null
  shownMonth.value = new Date(today.getFullYear(), today.getMonth(), 1)
}

// ── Search ────────────────────────────────────────────────

// A search lists the trips over its own days that match its words, in place of the month; the
// tiles and day totals then add up what it found.
const search = ref<LogSearchQuery | null>(null)

function searchTexts(entry: TripLogEntry): (string | null)[] {
  const ends: [TripLogEntry['startPlace'], number, number][] = [
    [entry.startPlace, entry.startLatitude, entry.startLongitude],
    [entry.endPlace, entry.endLatitude, entry.endLongitude],
  ]
  const texts: (string | null)[] = [
    entry.notes,
    t(`tripLog.purpose.${entry.purpose ?? 'unclassified'}`),
  ]
  for (const [place, lat, lng] of ends) {
    texts.push(postalAddressLabel(place), cityName(place), place?.displayName ?? null)
    const commute = commutePlaceAt(commutePlaces.places, lat, lng)
    if (commute) texts.push(commute.label, t(`tripLog.places.kind.${commute.kind}`))
  }
  return texts
}

function runSearch(query: LogSearchQuery) {
  search.value = query
  reload()
}

function clearSearch() {
  search.value = null
  reload()
}

const searchSummary = computed(() => {
  const query = search.value
  if (!query) return null
  const m = monthTotals.value
  const rate = homeRate.value
  const range = searchRange(query)
  return {
    found: t('tripLog.searchResults', m.trips),
    range: range.ok
      ? t('logSearch.range', {
          from: formatDate(range.from, { day: 'numeric', month: 'short', year: 'numeric' }),
          to: formatDate(new Date(range.to.getTime() - 1), {
            day: 'numeric',
            month: 'short',
            year: 'numeric',
          }),
        })
      : '',
    figures: [
      m.km > 0 ? units.value.format('distance', m.km, { decimals: 0 }) : null,
      m.kwh > 0 ? `${formatNumber(m.kwh, 1)} ${t('common.kwh')}` : null,
      rate !== null && m.kwh > 0 ? money(m.kwh * rate) : null,
    ].filter((f): f is string => !!f),
  }
})

// Hidden trips stay out of the list unless asked for, as they stay out of the totals and export.
const showHidden = ref(false)
const listed = computed(() => {
  const all = showHidden.value ? store.entries : store.visibleEntries
  const words = search.value?.text ?? ''
  return words ? all.filter((entry) => matchesText(searchTexts(entry), words)) : all
})

// Newest first on screen, where the trips still to classify are; the export stays in date order.
const newestFirst = computed(() => [...listed.value].reverse())

// Every trip but the newest has a saved trip after it to join with. The newest of a past month
// may too, in the month after; the server says so when there is none.
const newestId = computed(() => store.entries[store.entries.length - 1]?.id)

const placesShown = computed(() => uiSettings.placeNamesEnabled && store.placesAvailable)
const unclassified = computed(() => store.totals.unclassified.trips)
const unclassifiedDistance = computed(
  () => units.value.format('distance', store.totals.unclassified.km) ?? '',
)

// ── Totals ────────────────────────────────────────────────

const allTrips = computed(() => units.value.measure('distance', store.summary.km))

const purposeCards = computed(() =>
  PURPOSE_KEYS.map((key) => {
    const total = store.totals[key]
    const share = distanceShare(total, store.summary.km)
    return {
      key,
      label: t('tripLog.totalLabel', { purpose: t(`tripLog.purpose.${key}`), n: total.trips }),
      value: units.value.measure('distance', total.km)?.value ?? null,
      // Nothing left to classify is nothing to warn about.
      variant: key === 'unclassified' && total.trips === 0 ? undefined : PURPOSE_CARD_VARIANTS[key],
      share,
      note: t('tripLog.shareOfDistance', { percent: formatNumber(share * 100, 0) }),
    }
  }),
)

function measured(m: Measure | null) {
  return { value: m?.value ?? null, unit: m?.unit }
}

const summaryCards = computed(() => {
  const s = store.summary
  return [
    {
      key: 'drivingTime',
      icon: 'clock',
      label: t('tripLog.summary.drivingTime'),
      value: s.trips > 0 ? formatDurationMs(s.drivingMs, t) : null,
      unit: undefined,
      subtitle: undefined,
    },
    {
      key: 'averageTrip',
      icon: 'route',
      label: t('tripLog.summary.averageTrip'),
      ...measured(units.value.measure('distance', s.averageTripKm)),
      subtitle: undefined,
    },
    {
      key: 'longestTrip',
      icon: 'flag-checkered',
      label: t('tripLog.summary.longestTrip'),
      ...measured(units.value.measure('distance', s.longestTripKm)),
      subtitle: undefined,
    },
    {
      key: 'averageSpeed',
      icon: 'gauge',
      label: t('tripLog.summary.averageSpeed'),
      ...measured(units.value.measure('speed', s.averageSpeedKmh, { decimals: 0 })),
      subtitle: t('tripLog.summary.averageSpeedHint'),
    },
  ]
})

// ── The trip shown in detail ──────────────────────────────

// A trip opens in place, under its row; picking it again closes it.
const pickedId = ref<number | null>(null)

// Looked up among all trips, hidden ones too: a trip just hidden stays in view to show again.
const selected = computed(() =>
  pickedId.value === null ? null : (store.entries.find((e) => e.id === pickedId.value) ?? null),
)

const route = useTripRoute(vin, selected)

function toggle(id: number) {
  pickedId.value = pickedId.value === id ? null : id
}

// ── Days and figures ──────────────────────────────────────

const capacityKwh = computed(
  () => vehicleStore.hvBatteryCapacityKwh ?? vehicleStore.currentStatus?.hvTotalCapacityKwh ?? null,
)

function energyOf(entry: (typeof store.entries)[number]) {
  return tripEnergy(
    entry,
    loggedDistanceKm(entry),
    vehicleStore.effectiveVehicleType,
    capacityKwh.value,
  )
}

// Trips are reckoned at the cheapest home rate: most of what a car drives on was charged there.
const homeRate = computed(() => (uiSettings.tariff ? cheapestHomePrice(uiSettings.tariff) : null))

function money(amount: number) {
  return formatMoney(amount, uiSettings.tariff?.currency ?? 'GBP')
}

interface Totals {
  trips: number
  km: number
  ms: number
  kwh: number
  kwhKm: number
}

function totalsOf(entries: readonly (typeof store.entries)[number][]): Totals {
  const totals: Totals = { trips: 0, km: 0, ms: 0, kwh: 0, kwhKm: 0 }
  for (const e of entries) {
    if (e.isHidden) continue
    const km = loggedDistanceKm(e) ?? 0
    totals.trips++
    totals.km += km
    totals.ms += Date.parse(e.endedAt) - Date.parse(e.startedAt)
    const energy = energyOf(e)
    if (energy) {
      totals.kwh += energy.kwh
      totals.kwhKm += km
    }
  }
  return totals
}

function efficiencyOf(totals: Totals) {
  return totals.kwh > 0 && totals.kwhKm > 0
    ? units.value.measure('energyPerDistance', (totals.kwh * 1000) / totals.kwhKm)
    : null
}

const monthTotals = computed(() => totalsOf(listed.value))

const tiles = computed(() => {
  const m = monthTotals.value
  const distance = units.value.measure('distance', m.km, { decimals: 1 })
  const efficiency = efficiencyOf(m)
  const rate = homeRate.value
  return [
    {
      key: 'distance',
      value: distance?.value ?? '-',
      unit: distance?.unit,
      label: t('tripLog.tiles.distance'),
      note: null,
    },
    {
      key: 'trips',
      value: String(m.trips),
      unit: undefined,
      label: t('tripLog.tiles.trips'),
      note: null,
    },
    {
      key: 'efficiency',
      value: efficiency?.value ?? '-',
      unit: efficiency?.unit,
      label: t('tripLog.tiles.efficiency'),
      note: null,
    },
    {
      key: 'energy',
      value: m.kwh > 0 ? formatNumber(m.kwh, 1) : '-',
      unit: m.kwh > 0 ? t('common.kwh') : undefined,
      label: t('tripLog.tiles.energy'),
      note: null,
    },
    {
      key: 'cost',
      value: rate !== null && m.kwh > 0 ? money(m.kwh * rate) : '-',
      unit: undefined,
      label: t('tripLog.tiles.cost'),
      note: rate === null ? t('tripLog.tiles.noPrice') : null,
    },
  ]
})

const days = computed(() => groupByDay(newestFirst.value, (e) => e.startedAt))

function dayFigures(entries: readonly (typeof store.entries)[number][]) {
  const totals = totalsOf(entries)
  const oldest = entries[entries.length - 1]
  const newest = entries[0]
  const socStart = oldest?.socStartPercent
  const socEnd = newest?.socEndPercent
  const efficiency = efficiencyOf(totals)
  const rate = homeRate.value
  return {
    trips: t('tripLog.tripCount', totals.trips),
    soc:
      socStart != null && socEnd != null
        ? `${Math.round(socStart)}% → ${Math.round(socEnd)}%`
        : null,
    distance: units.value.format('distance', totals.km),
    duration: totals.ms > 0 ? formatDurationMs(totals.ms, t) : null,
    kwh: totals.kwh > 0 ? `${formatNumber(totals.kwh, 1)} ${t('common.kwh')}` : null,
    efficiency: efficiency ? `${efficiency.value} ${efficiency.unit}` : null,
    cost: rate !== null && totals.kwh > 0 ? money(totals.kwh * rate) : null,
  }
}

const calendarDays = computed(() => {
  const result: Record<string, CalendarDay> = {}
  const groups = days.value.filter(
    (d) =>
      d.date.getFullYear() === shownMonth.value.getFullYear() &&
      d.date.getMonth() === shownMonth.value.getMonth(),
  )
  const kms = groups.map((g) => totalsOf(g.items).km)
  const most = Math.max(1, ...kms)
  groups.forEach((g, i) => {
    result[g.key] = {
      value: units.value.format('distance', kms[i]!, { decimals: 0 }) ?? '',
      count: g.items.length,
      level: kms[i]! / most,
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
  document.getElementById(`trip-day-${key}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

// What the detail shows and does, the same beside the list and in the dialog.
const detail = computed(() => {
  const entry = selected.value
  if (!entry) return null
  return {
    props: {
      entry,
      routePoints: route.points.value,
      routeLoading: route.loading.value,
      routeError: route.error.value,
      placesShown: placesShown.value,
      placesResolving: store.placesResolving,
      saving: store.saving.has(entry.id),
      commutePlaces: commutePlaces.places,
      canJoinNext: entry.id !== newestId.value,
    },
    on: {
      save: (purpose: TripPurpose | null, notes: string | null) => save(entry.id, purpose, notes),
      pickPlace: openPlaces,
      setHidden: (hidden: boolean) => setHidden(entry.id, hidden),
      joinNext: () => {
        joiningId.value = entry.id
      },
    },
  }
})

// ── Loading ───────────────────────────────────────────────

function lookUpPlaces() {
  if (vin.value && placesShown.value) void store.resolvePlaces(vin.value, uiSettings.locale)
}

async function load() {
  await vehicleStore.fetchVehicles()
  if (!vin.value) return
  void commutePlaces.fetchPlaces(vin.value)
  await fetchEntries(vin.value)
  lookUpPlaces()
}

async function fetchEntries(v: string) {
  const range = search.value ? searchRange(search.value) : null
  if (range?.ok) await store.fetchRange(v, range.from, range.to)
  else await store.fetchLog(v, period.value)
}

// Another period or car shows other trips: the newest of those, not one picked before.
function reload() {
  pickedId.value = null
  selectedDay.value = null
  void load()
}

// ── Home and work ─────────────────────────────────────────

const placesOpen = ref(false)
const pick = ref<{ latitude: number; longitude: number } | null>(null)

const carPosition = computed<[number, number] | null>(() => {
  const status = vehicleStore.currentStatus
  return status?.latitude != null && status.longitude != null
    ? [status.latitude, status.longitude]
    : null
})

function openPlaces(latitude?: number, longitude?: number) {
  pick.value = latitude === undefined || longitude === undefined ? null : { latitude, longitude }
  placesOpen.value = true
}

// A place changed, so trips may have been labelled or unlabelled: show the log as it is now.
async function reloadLog() {
  if (!vin.value) return
  await fetchEntries(vin.value)
  lookUpPlaces()
}

onMounted(load)
watch(period, reload)
watch(vin, reload)

// Switching place names off stops the lookups and shows coordinates; switching them back on asks
// for whatever this page is still missing.
watch(
  () => uiSettings.placeNamesEnabled,
  (on) => {
    if (on) lookUpPlaces()
    else store.stopResolvingPlaces()
  },
)

onUnmounted(() => store.stopResolvingPlaces())

function save(id: number, purpose: TripPurpose | null, notes: string | null) {
  if (vin.value) void store.updateEntry(vin.value, id, purpose, notes)
}

function setHidden(id: number, hidden: boolean) {
  if (vin.value) void store.setHidden(vin.value, id, hidden)
}

// Joining cannot be undone, so it is asked for in a dialog rather than on the trip's own row.
const joiningId = ref<number | null>(null)
const joinBusy = computed(() => joiningId.value !== null && store.saving.has(joiningId.value))

function cancelJoin() {
  if (!joinBusy.value) joiningId.value = null
}

async function confirmJoin() {
  if (vin.value && joiningId.value !== null) await store.mergeWithNext(vin.value, joiningId.value)
  joiningId.value = null
}

function classifyAll(purpose: TripPurpose) {
  if (vin.value) void store.classifyUnclassified(vin.value, purpose)
}

function exportCsv() {
  const csv = tripLogCsv(store.visibleEntries, {
    t,
    units: units.value,
    placesShown: placesShown.value,
  })
  downloadCsv(tripLogFileName(period.value), csv)
}
</script>

<template>
  <div class="view-container">
    <ViewHeader :title="t('tripLog.title')" :subtitle="t('tripLog.subtitle')">
      <template #actions>
        <button type="button" class="btn btn-outline-secondary btn-sm" @click="openPlaces()">
          <font-awesome-icon icon="house" />{{ t('tripLog.places.button') }}
        </button>
        <button
          type="button"
          class="btn btn-primary btn-sm"
          :disabled="store.visibleEntries.length === 0"
          :title="store.placesResolving ? t('tripLog.exportWhileResolving') : undefined"
          @click="exportCsv"
        >
          <font-awesome-icon icon="file-csv" />{{ t('tripLog.export') }}
        </button>
      </template>
    </ViewHeader>

    <section class="stat-tiles" :aria-label="t('tripLog.totalsLabel')">
      <StatTile
        v-for="tile in tiles"
        :key="tile.key"
        :label="tile.label"
        :value="tile.value"
        :unit="tile.unit"
        :note="tile.note"
        :highlight="tile.key === 'distance'"
      />
    </section>

    <div v-if="unclassified > 0 && !store.loading && !search" class="trip-log-bulk">
      <span class="text-sm">
        <font-awesome-icon icon="circle-exclamation" class="trip-log-bulk__icon" />{{
          t(
            'tripLog.classifyAll',
            { n: unclassified, distance: unclassifiedDistance },
            unclassified,
          )
        }}
      </span>
      <div class="trip-log-bulk__actions">
        <button
          v-for="purpose in TRIP_PURPOSES"
          :key="purpose"
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="store.saving.size > 0"
          @click="classifyAll(purpose)"
        >
          <font-awesome-icon :icon="PURPOSE_ICONS[purpose]" />{{ t(`tripLog.purpose.${purpose}`) }}
        </button>
      </div>
    </div>

    <p v-if="!placesShown" class="text-xs text-muted">{{ t('tripLog.placesOff') }}</p>
    <p v-if="store.actionError" class="text-sm text-danger" role="alert">
      {{ errorMessage(store.actionError) }}
    </p>

    <WeekStrip
      :month="shownMonth"
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
          :placeholder="t('tripLog.searchPlaceholder')"
          @search="runSearch"
          @clear="clearSearch"
        />
        <MonthCalendar
          :month="shownMonth"
          :days="calendarDays"
          :selected="selectedDay"
          :can-next="canNextMonth"
          count-icon="car"
          :count-label="(n: number) => t('tripLog.tripCount', n)"
          @prev="stepMonth(-1)"
          @next="stepMonth(1)"
          @today="jumpToToday"
          @select="selectDay"
        />

        <div class="ll-card trip-log-side-stats">
          <dl class="ll-stats">
            <div>
              <dt>{{ t('tripLog.allTrips', { n: store.summary.trips }) }}</dt>
              <dd>{{ allTrips ? `${allTrips.value} ${allTrips.unit}` : '-' }}</dd>
            </div>
            <div v-for="card in purposeCards" :key="card.key">
              <dt><font-awesome-icon :icon="PURPOSE_ICONS[card.key]" /> {{ card.label }}</dt>
              <dd>{{ card.value ?? '-' }} {{ units.symbol('distance') }}</dd>
            </div>
            <div v-for="card in summaryCards" :key="card.key">
              <dt>{{ card.label }}</dt>
              <dd>{{ card.value ?? '-' }} {{ card.unit ?? '' }}</dd>
            </div>
          </dl>
        </div>

        <div class="ll-card trip-log-filters">
          <label class="trip-log-hidden-toggle trip-log-year-toggle text-sm">
            <input v-model="wholeYear" type="checkbox" class="form-check-input" />
            {{ t('tripLog.showWholeYear', { year: shownMonth.getFullYear() }) }}
          </label>
          <label v-if="store.hiddenCount > 0" class="trip-log-hidden-toggle text-sm">
            <input v-model="showHidden" type="checkbox" class="form-check-input" />
            {{ t('tripLog.showHidden', store.hiddenCount) }}
          </label>
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
        <div v-if="store.loadError" class="empty-state text-danger" role="alert">
          {{ errorMessage(store.loadError) }}
        </div>
        <div v-else-if="!store.loading && listed.length === 0 && !search" class="empty-state">
          {{ t('tripLog.empty') }}
        </div>
        <section
          v-for="day in days"
          v-else
          :id="`trip-day-${day.key}`"
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
            <span class="text-sm text-muted">{{ dayFigures(day.items).trips }}</span>
            <span class="ll-day__figures">
              <span v-if="dayFigures(day.items).soc">{{ dayFigures(day.items).soc }}</span>
              <strong v-if="dayFigures(day.items).distance">{{
                dayFigures(day.items).distance
              }}</strong>
              <span v-if="dayFigures(day.items).duration">{{
                dayFigures(day.items).duration
              }}</span>
              <span v-if="dayFigures(day.items).kwh">{{ dayFigures(day.items).kwh }}</span>
              <span v-if="dayFigures(day.items).efficiency" class="ll-pill ll-pill--good">{{
                dayFigures(day.items).efficiency
              }}</span>
              <span v-if="dayFigures(day.items).cost" class="ll-cost">{{
                dayFigures(day.items).cost
              }}</span>
            </span>
          </header>
          <ul class="ll-list trip-log-list">
            <TripLogItem
              v-for="entry in day.items"
              :key="entry.id"
              :entry="entry"
              :places-shown="placesShown"
              :places-resolving="store.placesResolving"
              :saving="store.saving.has(entry.id)"
              :commute-places="commutePlaces.places"
              :selected="selected?.id === entry.id"
              @select="toggle(entry.id)"
            >
              <div v-if="selected?.id === entry.id && detail" class="ll-detail">
                <TripLogDetail v-bind="detail.props" v-on="detail.on" />
              </div>
            </TripLogItem>
          </ul>
        </section>
      </div>
    </div>

    <DetailModal :open="joiningId !== null" :title="t('tripLog.joinNext')" @close="cancelJoin">
      <p class="text-sm">{{ t('tripLog.joinNextHint') }}</p>
      <template #footer>
        <button
          type="button"
          class="btn btn-outline-secondary"
          :disabled="joinBusy"
          @click="cancelJoin"
        >
          {{ t('common.cancel') }}
        </button>
        <button type="button" class="btn btn-warning" :disabled="joinBusy" @click="confirmJoin">
          <font-awesome-icon icon="link" />{{ t('tripLog.joinNextConfirm') }}
        </button>
      </template>
    </DetailModal>

    <CommutePlacesModal
      :open="placesOpen"
      :vin="vin"
      :pick="pick"
      :fallback-center="carPosition"
      @close="placesOpen = false"
      @changed="reloadLog"
    />
  </div>
</template>
