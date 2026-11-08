<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { tripLogApi, type TripLogEntry } from '@/services/tripLogApi'
import { useUnits } from '@/composables/useUnits'
import { loggedDistanceKm, endLabel, missingPlaces, PURPOSE_ICONS } from '@/utils/tripLog'
import { tripEnergy } from '@/utils/energy'
import { formatDateTime, formatNumber } from '@/utils/format'

// The last few trips, and what the past seven days added up to.
const { t } = useI18n()
const store = useVehicleStore()
const uiSettings = useUiSettingsStore()
const units = useUnits()

const trips = ref<TripLogEntry[]>([])
const loaded = ref(false)

const capacityKwh = computed(
  () => store.hvBatteryCapacityKwh ?? store.currentStatus?.hvTotalCapacityKwh ?? null,
)

async function load() {
  const vin = store.activeVin
  if (!vin) return
  try {
    const to = new Date()
    const from = new Date(to.getTime() - 14 * 86_400_000)
    const list = await tripLogApi.log(vin, from.toISOString(), to.toISOString())
    trips.value = list
      .filter((e) => !e.isHidden)
      .sort((a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt))
    void resolveShown(vin)
  } catch {
    trips.value = []
  } finally {
    loaded.value = true
  }
}

// The three trips shown get their addresses looked up once, as the trip log does; the server keeps
// them, so the next visit has them already.
async function resolveShown(vin: string) {
  const ids = trips.value
    .slice(0, 3)
    .filter(missingPlaces)
    .map((e) => e.id)
  if (ids.length === 0) return
  try {
    const result = await tripLogApi.resolvePlaces(vin, ids, uiSettings.locale)
    const found = new Map(result.trips.map((p) => [p.id, p]))
    trips.value = trips.value.map((e) => {
      const p = found.get(e.id)
      return p ? { ...e, startPlace: p.startPlace, endPlace: p.endPlace } : e
    })
  } catch {
    // Coordinates stay in place of the addresses.
  }
}

onMounted(load)
watch(() => store.activeVin, load)
watch(
  () => store.tripJustCompleted,
  (done) => {
    if (done) void load()
  },
)

function energyOf(e: TripLogEntry) {
  return tripEnergy(e, loggedDistanceKm(e), store.effectiveVehicleType, capacityKwh.value)
}

const recent = computed(() =>
  trips.value.slice(0, 3).map((e) => {
    const energy = energyOf(e)
    return {
      id: e.id,
      icon: PURPOSE_ICONS[e.purpose ?? 'unclassified'],
      route: `${endLabel(e.startPlace, e.startLatitude, e.startLongitude)} → ${endLabel(e.endPlace, e.endLatitude, e.endLongitude)}`,
      when: formatDateTime(e.startedAt, { weekday: 'short', hour: '2-digit', minute: '2-digit' }),
      distance: units.value.format('distance', loggedDistanceKm(e), { decimals: 1 }),
      efficiency:
        energy?.whPerKm != null ? units.value.format('energyPerDistance', energy.whPerKm) : null,
    }
  }),
)

const week = computed(() => {
  const since = Date.now() - 7 * 86_400_000
  const list = trips.value.filter((e) => Date.parse(e.startedAt) >= since)
  if (list.length === 0) return null
  const km = list.reduce((s, e) => s + loggedDistanceKm(e), 0)
  let kwh = 0
  let kwhKm = 0
  for (const e of list) {
    const energy = energyOf(e)
    if (energy) {
      kwh += energy.kwh
      kwhKm += loggedDistanceKm(e)
    }
  }
  return {
    distance: units.value.format('distance', km, { decimals: 1 }),
    trips: t('overview.trips.count', list.length),
    energy: kwh > 0 ? `${formatNumber(kwh, 1)} ${t('common.kwh')}` : null,
    efficiency: kwhKm > 0 ? units.value.format('energyPerDistance', (kwh * 1000) / kwhKm) : null,
  }
})
</script>

<template>
  <section class="ov-card ov-trips" :aria-label="t('overview.trips.title')">
    <header class="ov-card__head">
      <h2 class="ov-card__title">{{ t('overview.trips.title') }}</h2>
      <RouterLink to="/trip-log" class="ov-card__link">{{ t('overview.trips.all') }}</RouterLink>
    </header>
    <ul v-if="recent.length > 0" class="ov-trips__list">
      <li v-for="trip in recent" :key="trip.id">
        <RouterLink to="/trip-log" class="ov-trip">
          <span class="ov-trip__icon"><font-awesome-icon :icon="trip.icon" /></span>
          <span class="ov-trip__text">
            <b>{{ trip.route }}</b>
            <small>{{ trip.when }}</small>
          </span>
          <span class="ov-trip__figures">
            <b>{{ trip.distance }}</b>
            <small v-if="trip.efficiency">{{ trip.efficiency }}</small>
          </span>
        </RouterLink>
      </li>
    </ul>
    <p v-else-if="loaded" class="text-muted text-sm">{{ t('overview.trips.none') }}</p>
    <p v-if="week" class="ov-trips__week">
      <span class="ov-card__label">{{ t('overview.trips.week') }}</span>
      <span>{{ week.distance }} · {{ week.trips }}</span>
      <span v-if="week.energy">{{ week.energy }}</span>
      <span v-if="week.efficiency" class="text-success">{{ week.efficiency }}</span>
    </p>
  </section>
</template>
