<script setup lang="ts">
import '@/assets/logList.css'
import { computed, ref, toRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  tripLogApi,
  TRIP_NOTES_MAX_LENGTH,
  TRIP_PURPOSES,
  type TripLogEntry,
  type TripPurpose,
  type TripTelemetryPoint,
} from '@/services/tripLogApi'
import type { ChartData, ChartOptions } from 'chart.js'
import type { CommutePlace } from '@/services/commutePlacesApi'
import type { TripPoint } from '@/services/vehicleApi'
import { formatDate, formatNumber, formatTime, numberLocale } from '@/utils/format'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { cheapestHomePrice, formatMoney } from '@/utils/tariff'
import {
  buildGpx,
  downloadText,
  drivingPowerSign,
  summarizeTripTelemetry,
} from '@/utils/tripTelemetry'
import { downsample } from '@/utils/downsample'
import { useChartTheme } from '@/composables/useChartTheme'
import { tripEnergy } from '@/utils/energy'
import { useVehicleStore } from '@/stores/vehicle'
import { formatTripDuration } from '@/utils/tripRows'
import {
  loggedDistanceKm,
  odometerDistanceKm,
  PURPOSE_BADGE_CLASSES,
  PURPOSE_ICONS,
} from '@/utils/tripLog'
import { COMMUTE_PLACE_ICONS } from '@/utils/commutePlaces'
import type { ChipOption } from '@/utils/chips'
import { useUnits } from '@/composables/useUnits'
import { useTripEnds } from '@/composables/useTripEnds'
import { useTripSpeedChart } from '@/composables/useTripSpeedChart'
import ChipGroup from './ChipGroup.vue'
import StatsChartCard from './StatsChartCard.vue'
import TripRouteMap from './TripRouteMap.vue'

const props = defineProps<{
  entry: TripLogEntry
  /** The route's fixes, for the map and the speed chart; null while loading or unavailable. */
  routePoints: readonly TripPoint[] | null
  routeLoading: boolean
  /** Why the route could not be loaded, already translated. */
  routeError: string | null
  /** False when place names are switched off: the ends then read as coordinates. */
  placesShown: boolean
  /** True while addresses are being looked up, so a missing one shows a placeholder. */
  placesResolving: boolean
  saving: boolean
  /** Home and work, to mark the ends that are at one of them. */
  commutePlaces: readonly CommutePlace[]
  /** A saved trip follows this one, so it can be joined with it. */
  canJoinNext: boolean
}>()

const emit = defineEmits<{
  (e: 'save', purpose: TripPurpose | null, notes: string | null): void
  /** One of the trip's ends, to set as home or work. */
  (e: 'pickPlace', latitude: number, longitude: number): void
  (e: 'setHidden', hidden: boolean): void
  /** Asks to join with the trip after it; the page confirms, since it cannot be undone. */
  (e: 'joinNext'): void
}>()

const { t } = useI18n()
const units = useUnits()
const vehicleStore = useVehicleStore()
const uiSettings = useUiSettingsStore()
const chartTheme = useChartTheme()

// What the car reported while driving: regen, climb, outside temperature, climate and the chart.
const telemetry = ref<TripTelemetryPoint[]>([])
watch(
  () => props.entry.id,
  async (id) => {
    telemetry.value = []
    const vin = vehicleStore.activeVin
    if (!vin) return
    try {
      const points = await tripLogApi.telemetry(vin, id)
      if (props.entry.id === id) telemetry.value = points
    } catch {
      // The extra figures are left out; the rest of the detail stands on its own.
    }
  },
  { immediate: true },
)
const telemetrySummary = computed(() => summarizeTripTelemetry(telemetry.value))

const { from, to } = useTripEnds({
  entry: toRef(props, 'entry'),
  placesShown: toRef(props, 'placesShown'),
  placesResolving: toRef(props, 'placesResolving'),
  commutePlaces: toRef(props, 'commutePlaces'),
})

const dateLabel = computed(() =>
  formatDate(props.entry.startedAt, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }),
)

const timeLabel = computed(
  () => `${formatTime(props.entry.startedAt)} - ${formatTime(props.entry.endedAt)}`,
)

const durationLabel = computed(() =>
  formatTripDuration(props.entry.startedAt, props.entry.endedAt, t),
)

const purposeKey = computed(() => props.entry.purpose ?? 'unclassified')

// The odometer at either end, which the tax log records; the list leaves it to this detail.
const odometerLabel = computed(() => {
  const start = units.value.measure('distance', props.entry.odometerStartKm)
  const finish = units.value.measure('distance', props.entry.odometerEndKm)
  return start && finish ? `${start.value} - ${finish.value}` : null
})

interface Figure {
  key: string
  icon: string
  label: string
  measure: { value: string; unit: string } | null
  title: string | undefined
  note: string | null
  /** What the note is, for its tooltip, and the icon in front of it. */
  noteTitle?: string
  noteIcon?: string
}

// What the trip took out of the battery: the car's own count, or failing that the drop in state
// of charge scaled by the battery's capacity, configured or as the car reports it.
const energy = computed(() =>
  tripEnergy(
    props.entry,
    loggedDistanceKm(props.entry),
    vehicleStore.effectiveVehicleType,
    vehicleStore.hvBatteryCapacityKwh ?? vehicleStore.currentStatus?.hvTotalCapacityKwh ?? null,
  ),
)

const batteryLabel = computed(() => {
  const { socStartPercent: start, socEndPercent: end } = props.entry
  return start != null && end != null
    ? t('tripLog.detail.battery', { start: Math.round(start), end: Math.round(end) })
    : null
})

const energyFigures = computed((): Figure[] => {
  const figure = energy.value
  if (!figure) return []
  const { socStartPercent: start, socEndPercent: end } = props.entry
  return [
    {
      key: 'energy',
      icon: 'plug-circle-bolt',
      label: t('tripLog.detail.energy'),
      measure: { value: formatNumber(figure.kwh, figure.kwh < 10 ? 2 : 1), unit: t('common.kwh') },
      title:
        figure.source === 'car'
          ? t('tripLog.detail.energyFromCar')
          : t('tripLog.detail.energyFromSoc', {
              start: Math.round(start ?? 0),
              end: Math.round(end ?? 0),
            }),
      note: batteryLabel.value,
      noteIcon: 'battery-half',
    },
    {
      key: 'efficiency',
      icon: 'leaf',
      label: t('tripLog.detail.efficiency'),
      measure: units.value.measure('energyPerDistance', figure.whPerKm),
      title: undefined,
      note: null,
    },
  ]
})

function climbLabel(metres: number): string {
  const feet = uiSettings.effectiveUnits.distance === 'mi'
  return formatNumber(feet ? metres * 3.28084 : metres, 0)
}

const telemetryFigures = computed((): Figure[] => {
  const s = telemetrySummary.value
  const list: Figure[] = []
  if (s.regenKwh !== null && s.usedKwh !== null) {
    list.push({
      key: 'regen',
      icon: 'rotate-left',
      label: t('tripLog.detail.regen'),
      measure: { value: formatNumber(s.regenKwh, 2), unit: t('common.kwh') },
      title: t('tripLog.detail.regenHint'),
      note: t('tripLog.detail.regenShare', {
        percent: Math.round((s.regenKwh / s.usedKwh) * 100),
      }),
      noteIcon: 'leaf',
    })
  }
  const tariff = uiSettings.tariff
  const price = tariff ? cheapestHomePrice(tariff) : null
  if (energy.value && tariff && price !== null) {
    list.push({
      key: 'cost',
      icon: 'coins',
      label: t('tripLog.detail.cost'),
      measure: { value: formatMoney(energy.value.kwh * price, tariff.currency), unit: '' },
      title: undefined,
      note: t('tripLog.detail.costAt', { rate: formatMoney(price, tariff.currency, 3) }),
      noteIcon: 'plug-circle-bolt',
    })
  }
  if (s.climbM !== null && s.descentM !== null) {
    list.push({
      key: 'climb',
      icon: 'mountain',
      label: t('tripLog.detail.climb'),
      measure: {
        value: `↑ ${climbLabel(s.climbM)} ↓ ${climbLabel(s.descentM)}`,
        unit: uiSettings.effectiveUnits.distance === 'mi' ? 'ft' : 'm',
      },
      title: undefined,
      note: null,
    })
  }
  if (s.outsideStart !== null && s.outsideEnd !== null) {
    const start = units.value.measure('temperature', s.outsideStart, { decimals: 0 })
    const end = units.value.measure('temperature', s.outsideEnd, { decimals: 0 })
    list.push({
      key: 'outside',
      icon: 'temperature-half',
      label: t('tripLog.detail.outside'),
      measure: start && end ? { value: `${start.value} → ${end.value}`, unit: end.unit } : null,
      title: undefined,
      note: null,
    })
  }
  if (s.climateMinutes !== null) {
    list.push({
      key: 'climate',
      icon: 'fan',
      label: t('tripLog.detail.climate'),
      measure: {
        value:
          s.climateMinutes < 1
            ? t('tripLog.detail.climateOff')
            : t('tripLog.detail.climateOn', { minutes: Math.round(s.climateMinutes) }),
        unit: '',
      },
      title: undefined,
      note: null,
    })
  }
  return list
})

const figures = computed((): Figure[] => [
  {
    key: 'distance',
    icon: 'road',
    label: t('tripLog.detail.distance'),
    measure: units.value.measure('distance', loggedDistanceKm(props.entry)),
    title:
      odometerDistanceKm(props.entry) !== null
        ? t('tripLog.distanceFromOdometer')
        : t('tripLog.distanceFromGps'),
    note: odometerLabel.value,
    noteTitle: t('tripLog.odometer'),
    noteIcon: 'gauge',
  },
  {
    key: 'duration',
    icon: 'clock',
    label: t('tripLog.detail.duration'),
    measure: { value: durationLabel.value, unit: '' },
    title: undefined,
    note: null,
  },
  {
    key: 'avgSpeed',
    icon: 'gauge',
    label: t('tripLog.detail.averageSpeed'),
    measure: units.value.measure('speed', props.entry.avgMovingSpeedKmh, { decimals: 0 }),
    title: t('tripLog.detail.averageSpeedHint'),
    note: null,
  },
  {
    key: 'topSpeed',
    icon: 'gauge-high',
    label: t('tripLog.detail.topSpeed'),
    measure: units.value.measure('speed', props.entry.maxSpeedKmh, { decimals: 0 }),
    title: undefined,
    note: null,
  },
  ...energyFigures.value,
  ...telemetryFigures.value,
])

const MAX_TELEMETRY_CHART_POINTS = 300

// Speed, power, battery level and elevation over the trip, each in a band of its own on one time
// axis, so a slow-down lines up with the regen it brought and the hill it was on.
const telemetryChart = computed(() => {
  const all = telemetry.value
  if (all.length < 3) return null
  const has = (pick: (p: TripTelemetryPoint) => number | null) => all.some((p) => pick(p) !== null)
  const hasSpeed = has((p) => p.speed)
  const hasPower = has((p) => p.powerKw)
  const hasSoc = has((p) => p.socPercent)
  const hasElevation = has((p) => p.elevation)
  if (!hasSpeed && !hasPower && !hasSoc) return null

  const shown = downsample([...all], MAX_TELEMETRY_CHART_POINTS)
  const sign = drivingPowerSign(all)
  const feet = uiSettings.effectiveUnits.distance === 'mi'
  const { text, grid } = chartTheme.value
  const band = (weight: number, color: string, title: string, extra = {}) => ({
    type: 'linear' as const,
    position: 'left' as const,
    stack: 'trip',
    stackWeight: weight,
    offset: true,
    grid: { color: grid },
    border: { color: grid },
    ticks: { color, maxTicksLimit: 4 },
    title: { display: true, text: title, color, font: { size: 10 } },
    ...extra,
  })

  const datasets: ChartData<'line'>['datasets'] = []
  const scales: ChartOptions<'line'>['scales'] = {
    x: {
      grid: { color: grid },
      ticks: { color: text, maxRotation: 0, autoSkip: true, maxTicksLimit: 7 },
    },
  }
  // Chart.js lays a stack out from the bottom up, so the bands are added bottom first.
  if (hasElevation) {
    scales.elevation = band(1, '#c08a4a', feet ? 'ft' : 'm')
    datasets.push({
      label: t('tripLog.detail.elevation'),
      data: shown.map((p) =>
        p.elevation === null ? null : feet ? p.elevation * 3.28084 : p.elevation,
      ),
      borderColor: '#c08a4a',
      backgroundColor: 'rgba(192,138,74,0.15)',
      fill: true,
      yAxisID: 'elevation',
    })
  }
  if (hasSoc) {
    scales.soc = band(1, '#22c55e', '%')
    datasets.push({
      label: t('tripLog.detail.batteryLevel'),
      data: shown.map((p) => p.socPercent),
      borderColor: '#22c55e',
      yAxisID: 'soc',
    })
  }
  if (hasPower) {
    scales.power = band(1.4, '#ef4444', 'kW')
    datasets.push({
      label: t('tripLog.detail.power'),
      data: shown.map((p) => (p.powerKw === null ? null : Math.round(p.powerKw * sign * 10) / 10)),
      borderColor: '#ef4444',
      borderWidth: 1.25,
      yAxisID: 'power',
    })
  }
  if (hasSpeed) {
    scales.speed = band(1.6, '#3b82f6', units.value.symbol('speed'), { min: 0 })
    datasets.push({
      label: t('tripLog.detail.speed'),
      data: shown.map((p) =>
        p.speed === null ? null : Math.round(units.value.convert('speed', p.speed)),
      ),
      borderColor: '#3b82f6',
      borderWidth: 1.25,
      yAxisID: 'speed',
    })
  }
  for (const d of datasets) {
    Object.assign(d, { pointRadius: 0, spanGaps: true, tension: 0.2 })
  }
  // The legend reads top to bottom, as the bands do.
  datasets.reverse()

  const data: ChartData<'line'> = { labels: shown.map((p) => formatTime(p.recordedAt)), datasets }
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

// The figures the panel lists under the summary, like a trip computer would: only those known.
const details = computed(() => {
  const s = telemetrySummary.value
  const e = props.entry
  const rows: { key: string; label: string; value: string; note?: string }[] = []
  const { socStartPercent: socStart, socEndPercent: socEnd } = e
  if (socStart != null && socEnd != null) {
    const delta = Math.round(socEnd - socStart)
    rows.push({
      key: 'soc',
      label: t('tripLog.detail.soc'),
      value: `${Math.round(socStart)}% → ${Math.round(socEnd)}%`,
      note: `(${delta > 0 ? '+' : ''}${delta}%)`,
    })
  }
  const odoStart = units.value.format('distance', e.odometerStartKm ?? null, {
    decimals: 0,
    grouped: true,
  })
  const odoEnd = units.value.format('distance', e.odometerEndKm ?? null, {
    decimals: 0,
    grouped: true,
  })
  if (odoStart && odoEnd) {
    rows.push({ key: 'odometer', label: t('tripLog.odometer'), value: `${odoStart} → ${odoEnd}` })
  }
  const sign = drivingPowerSign(telemetry.value)
  const powers = telemetry.value
    .map((p) => (p.powerKw === null ? null : p.powerKw * sign))
    .filter((v): v is number => v !== null)
  if (powers.length > 0) {
    rows.push({
      key: 'maxPower',
      label: t('tripLog.detail.maxPower'),
      value: `${formatNumber(Math.max(...powers), 0)} kW`,
    })
    const regen = Math.min(...powers)
    if (regen < 0) {
      rows.push({
        key: 'maxRegen',
        label: t('tripLog.detail.maxRegen'),
        value: `${formatNumber(-regen, 0)} kW`,
      })
    }
  }
  if (s.climbM !== null && s.descentM !== null) {
    const unit = uiSettings.effectiveUnits.distance === 'mi' ? 'ft' : 'm'
    rows.push({
      key: 'climb',
      label: t('tripLog.detail.ascentDescent'),
      value: `↑ ${climbLabel(s.climbM)} ${unit}  ↓ ${climbLabel(s.descentM)} ${unit}`,
    })
  }
  if (s.outsideStart !== null && s.outsideEnd !== null) {
    const lo = units.value.measure('temperature', Math.min(s.outsideStart, s.outsideEnd), {
      decimals: 1,
    })
    const hi = units.value.measure('temperature', Math.max(s.outsideStart, s.outsideEnd), {
      decimals: 1,
    })
    if (lo && hi) {
      rows.push({
        key: 'outside',
        label: t('tripLog.detail.outside'),
        value: `${lo.value} – ${hi.value} ${hi.unit}`,
      })
    }
  }
  if (s.climateMinutes !== null) {
    rows.push({
      key: 'climate',
      label: t('tripLog.detail.climate'),
      value:
        s.climateMinutes < 1
          ? t('tripLog.detail.climateOff')
          : t('tripLog.detail.climateOn', { minutes: Math.round(s.climateMinutes) }),
    })
  }
  if (props.routePoints && props.routePoints.length > 0) {
    rows.push({
      key: 'points',
      label: t('tripLog.detail.gpsPoints'),
      value: String(props.routePoints.length),
    })
  }
  return rows
})

function downloadGpx() {
  const points = props.routePoints
  if (!points || points.length === 0) return
  const name = `${dateLabel.value} ${timeLabel.value}`
  const stamp = props.entry.startedAt.slice(0, 16).replace(/[:T]/g, '-')
  downloadText(
    `garagestack-trip-${stamp}.gpx`,
    buildGpx(name, points, telemetry.value),
    'application/gpx+xml',
  )
}

const speedChart = useTripSpeedChart(toRef(props, 'routePoints'))

// The field is edited locally and sent when it loses focus, rather than on every keystroke.
const notesDraft = ref(props.entry.notes ?? '')
watch(
  () => [props.entry.id, props.entry.notes] as const,
  ([, notes]) => {
    notesDraft.value = notes ?? ''
  },
)

function normalizedNotes(): string | null {
  const trimmed = notesDraft.value.trim()
  return trimmed === '' ? null : trimmed
}

// Each purpose's modifier class sets its own chip colour (tripLog.css).
const purposeOptions = computed((): ChipOption<TripPurpose>[] =>
  TRIP_PURPOSES.map((purpose) => ({
    value: purpose,
    label: t(`tripLog.purpose.${purpose}`),
    icon: PURPOSE_ICONS[purpose],
    class: `trip-log-purpose trip-log-purpose--${purpose}`,
  })),
)

// Choosing the purpose a trip already has clears it again, back to unclassified.
function choosePurpose(purpose: TripPurpose | null) {
  emit('save', purpose, props.entry.notes)
}

function saveNotes() {
  const notes = normalizedNotes()
  if (notes === props.entry.notes) return
  emit('save', props.entry.purpose, notes)
}

const notesId = computed(() => `trip-log-notes-${props.entry.id}`)
</script>

<template>
  <article
    class="trip-log-detail"
    :class="{ 'trip-log-detail--saving': saving, 'trip-log-detail--hidden': entry.isHidden }"
  >
    <header class="trip-log-detail__header">
      <h2 class="trip-log-detail__title">{{ dateLabel }}</h2>
      <span class="text-sm text-muted">{{ timeLabel }}</span>
      <div class="trip-log-detail__status">
        <span class="trip-log-detail__badge" :class="PURPOSE_BADGE_CLASSES[purposeKey]">
          <font-awesome-icon :icon="PURPOSE_ICONS[purposeKey]" />{{
            t(`tripLog.purpose.${purposeKey}`)
          }}
        </span>
        <span v-if="entry.purposeIsAutomatic" class="trip-log-detail__mark text-xs text-muted">
          <font-awesome-icon icon="wand-magic-sparkles" />{{ t('tripLog.places.automaticHint') }}
        </span>
        <span
          v-if="entry.isEdited"
          class="trip-log-detail__mark trip-log-detail__edited text-xs text-muted"
        >
          <font-awesome-icon icon="pen-to-square" />{{ t('tripLog.editedHint') }}
        </span>
      </div>
    </header>

    <div class="trip-detail__grid">
      <div class="trip-detail__col">
        <section class="trip-detail__card">
          <h3 class="trip-detail__card-title">
            <font-awesome-icon icon="location-dot" />{{ t('tripLog.detail.summary') }}
          </h3>
          <div class="trip-log-detail__ends">
            <template v-for="end in [from, to]" :key="end === from ? 'from' : 'to'">
              <span
                v-if="end.pending"
                class="skeleton skeleton--text skeleton--text-md"
                :aria-label="t('tripLog.resolvingPlaces')"
              />
              <button
                v-else
                type="button"
                class="trip-log-detail__place"
                :class="
                  end === from ? 'trip-log-detail__place--start' : 'trip-log-detail__place--end'
                "
                :title="t('tripLog.places.useEnd', { place: end.label })"
                :aria-label="t('tripLog.places.useEnd', { place: end.label })"
                @click="emit('pickPlace', end.lat, end.lng)"
              >
                <font-awesome-icon
                  v-if="end.commutePlace"
                  :icon="COMMUTE_PLACE_ICONS[end.commutePlace.kind]"
                  class="trip-log-detail__place-icon"
                />{{ end.label }}
              </button>
            </template>
          </div>
          <dl
            class="trip-log-detail__figures"
            :class="{ 'trip-log-detail__figures--six': figures.length > 4 }"
          >
            <div
              v-for="figure in figures"
              :key="figure.key"
              class="trip-log-detail__figure"
              :title="figure.title"
            >
              <dt class="trip-log-detail__figure-label">
                <font-awesome-icon :icon="figure.icon" aria-hidden="true" />{{ figure.label }}
              </dt>
              <dd class="trip-log-detail__figure-value">
                {{ figure.measure?.value ?? '-'
                }}<span v-if="figure.measure?.unit" class="trip-log-detail__figure-unit">
                  {{ figure.measure.unit }}</span
                >
              </dd>
              <dd
                v-if="figure.note"
                class="trip-log-detail__figure-note text-xs text-muted"
                :title="figure.noteTitle"
              >
                <font-awesome-icon :icon="figure.noteIcon ?? 'gauge'" aria-hidden="true" />
                {{ figure.note }}
              </dd>
            </div>
          </dl>
        </section>

        <section v-if="details.length" class="trip-detail__card">
          <h3 class="trip-detail__card-title">{{ t('tripLog.detail.tripDetails') }}</h3>
          <dl class="trip-detail__list">
            <div v-for="row in details" :key="row.key">
              <dt>{{ row.label }}</dt>
              <dd>
                {{ row.value }}<span v-if="row.note" class="text-muted"> {{ row.note }}</span>
              </dd>
            </div>
          </dl>
        </section>

        <section class="trip-detail__card">
          <section class="trip-log-detail__classify" :aria-label="t('tripLog.purposeLabel')">
            <ChipGroup
              class="trip-log-detail__purpose"
              :options="purposeOptions"
              :model-value="entry.purpose"
              :group-label="t('tripLog.purposeLabel')"
              :disabled="saving"
              deselectable
              @update:model-value="choosePurpose"
            />

            <label :for="notesId" class="text-xs text-muted">{{ t('tripLog.notesLabel') }}</label>
            <input
              :id="notesId"
              v-model="notesDraft"
              type="text"
              class="trip-log-detail__notes"
              :maxlength="TRIP_NOTES_MAX_LENGTH"
              :placeholder="t('tripLog.notesPlaceholder')"
              :disabled="saving"
              @change="saveNotes"
              @keydown.enter="($event.target as HTMLInputElement).blur()"
            />
          </section>
        </section>
      </div>

      <div class="trip-detail__col">
        <TripRouteMap
          :key="entry.id"
          :points="routePoints"
          :loading="routeLoading"
          :start="[entry.startLatitude, entry.startLongitude]"
          :end="[entry.endLatitude, entry.endLongitude]"
          :trip-id="entry.id"
          :started-at="entry.startedAt"
          :link-to-map="!entry.isHidden"
        />
        <p v-if="routeError" class="text-xs text-muted" role="status">
          {{ t('tripLog.detail.routeUnavailable') }}
        </p>

        <section v-if="telemetryChart" class="trip-detail__card trip-detail__chart">
          <h3 class="trip-detail__card-title">{{ t('tripLog.detail.tripData') }}</h3>
          <div class="trip-detail__chart-canvas ll-chart">
            <StatsChartCard
              :title="''"
              icon="chart-line"
              type="line"
              :data="telemetryChart.data"
              :options="telemetryChart.options"
              :show-info="false"
            />
          </div>
        </section>
        <StatsChartCard
          v-else-if="speedChart"
          class="trip-log-detail__chart"
          :title="t('tripLog.detail.speedChart')"
          icon="chart-line"
          type="line"
          :data="speedChart.data"
          :options="speedChart.options"
          :show-info="false"
        />

        <div class="trip-log-detail__actions">
          <!-- A hidden trip is left off the map, so there is nothing there to show it on. -->
          <RouterLink
            v-if="!entry.isHidden"
            class="btn btn-outline-secondary btn-sm"
            :to="{ name: 'map', query: { trip: entry.id, at: entry.startedAt } }"
          >
            <font-awesome-icon icon="map" />{{ t('tripLog.showOnMap') }}
          </RouterLink>
          <RouterLink
            class="btn btn-outline-secondary btn-sm"
            :to="{ name: 'tripEdit', params: { id: entry.id } }"
          >
            <font-awesome-icon icon="pen-to-square" />{{ t('tripLog.editRoute') }}
          </RouterLink>
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm"
            :disabled="!routePoints || routePoints.length === 0"
            @click="downloadGpx"
          >
            <font-awesome-icon icon="download" />{{ t('tripLog.downloadGpx') }}
          </button>
          <button
            v-if="canJoinNext"
            type="button"
            class="btn btn-outline-secondary btn-sm"
            :disabled="saving"
            :title="t('tripLog.joinNextHint')"
            @click="emit('joinNext')"
          >
            <font-awesome-icon icon="link" />{{ t('tripLog.joinNext') }}
          </button>
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm trip-log-detail__hide"
            :disabled="saving"
            :title="entry.isHidden ? undefined : t('tripLog.hideHint')"
            @click="emit('setHidden', !entry.isHidden)"
          >
            <font-awesome-icon :icon="entry.isHidden ? 'eye' : 'eye-slash'" />{{
              entry.isHidden ? t('tripLog.show') : t('tripLog.hide')
            }}
          </button>
        </div>
      </div>
    </div>
  </article>
</template>
