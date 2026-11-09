<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { LCircle, LCircleMarker, LMap, LMarker } from '@vue-leaflet/vue-leaflet'
import type { LatLng as LeafletLatLng, LeafletMouseEvent } from 'leaflet'
import RangeControl from '@/components/RangeControl.vue'
import SettingsToggle from '@/components/SettingsToggle.vue'
import { useCommutePlacesStore } from '@/stores/commutePlaces'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { useVehicleStore } from '@/stores/vehicle'
import { COMMUTE_RADIUS } from '@/services/commutePlacesApi'
import { chargesApi, type ChargeSession } from '@/services/chargesApi'
import { DEFAULT_MAP_CENTER, type LeafletMap } from '@/utils/leaflet'
import { distanceKm } from '@/utils/geo'
import { useLeafletMap } from '@/composables/useLeafletMap'
import { useBasemap } from '@/composables/useBasemap'
import { useErrorMessage } from '@/composables/useErrorMessage'

// Home, as the commute place of kind home that Trips > Places also edits: set here with a pin and
// a radius, so a charge that ends inside it counts as a home charge (and is priced as one).
const { t } = useI18n()
const errorMessage = useErrorMessage()
const places = useCommutePlacesStore()
const uiSettings = useUiSettingsStore()
const vehicleStore = useVehicleStore()

const vin = computed(() => vehicleStore.activeVin)
const home = computed(() => places.places.find((p) => p.kind === 'home') ?? null)

const carPosition = computed<[number, number] | null>(() => {
  const status = vehicleStore.currentStatus
  return status?.latitude != null && status.longitude != null
    ? [status.latitude, status.longitude]
    : null
})

interface Draft {
  latitude: number
  longitude: number
  radiusMeters: number
}

function fromSaved(): Draft {
  const h = home.value
  if (h) return { latitude: h.latitude, longitude: h.longitude, radiusMeters: h.radiusMeters }
  const [latitude, longitude] = carPosition.value ?? DEFAULT_MAP_CENTER
  return { latitude, longitude, radiusMeters: COMMUTE_RADIUS.default }
}

const draft = ref<Draft>(fromSaved())
// Fixed while the pin moves, so the map does not pan out from under the pointer.
const mapCenter = ref<[number, number]>([draft.value.latitude, draft.value.longitude])
const busy = ref(false)
// Moved by hand since the last save or cancel: then nothing loaded later moves it back.
const touched = ref(false)
const error = ref<string | null>(null)
const done = ref<string | null>(null)

const { mapInstance, bindMapReady } = useLeafletMap()
useBasemap(mapInstance)

const changed = computed(() => {
  const h = home.value
  if (!h) return true
  return (
    h.latitude !== draft.value.latitude ||
    h.longitude !== draft.value.longitude ||
    h.radiusMeters !== draft.value.radiusMeters
  )
})

function reset() {
  draft.value = fromSaved()
  touched.value = false
  recenter()
}

function recenter() {
  const at: [number, number] = [draft.value.latitude, draft.value.longitude]
  mapCenter.value = at
  mapInstance.value?.setView(at, mapInstance.value.getZoom())
}

// Follows the saved home (loaded late, or changed in Trips > Places), and without one the car's
// position once it is known, until moved by hand here.
watch([home, carPosition], () => {
  if (!busy.value && !touched.value) reset()
})

function moveTo(latLng: LeafletLatLng) {
  draft.value = { ...draft.value, latitude: latLng.lat, longitude: latLng.lng }
  touched.value = true
  done.value = null
}

function onMapClick(event: LeafletMouseEvent) {
  moveTo(event.latlng)
}

function onMapReady(map: LeafletMap) {
  bindMapReady(map)
}

function useCarPosition() {
  const at = carPosition.value
  if (!at) return
  draft.value = { ...draft.value, latitude: at[0], longitude: at[1] }
  touched.value = true
  done.value = null
  recenter()
}

const pin = computed<[number, number]>(() => [draft.value.latitude, draft.value.longitude])
const coordinates = computed(
  () => `${draft.value.latitude.toFixed(5)}, ${draft.value.longitude.toFixed(5)}`,
)

async function save() {
  if (!vin.value || busy.value) return
  error.value = null
  done.value = null
  busy.value = true
  const h = home.value
  const change = await places.savePlace(
    vin.value,
    {
      kind: 'home',
      label: h?.label ?? null,
      latitude: draft.value.latitude,
      longitude: draft.value.longitude,
      radiusMeters: draft.value.radiusMeters,
      notify: h?.notify ?? false,
    },
    h?.id,
  )
  busy.value = false
  if (change) {
    done.value = t('settings.home.saved')
    reset()
  } else if (places.actionError) {
    error.value = errorMessage(places.actionError)
  }
}

// ── Past charges ─────────────────────────────────────────

const charges = ref<ChargeSession[]>([])
const resetting = ref(false)

async function loadCharges() {
  if (!vin.value) return
  const to = new Date()
  const from = new Date(to.getTime() - 365 * 24 * 3600 * 1000)
  try {
    charges.value = await chargesApi.log(vin.value, from.toISOString(), to.toISOString())
  } catch {
    charges.value = []
  }
}

// Settings is often opened straight from a link, before any other page has loaded the car.
onMounted(() => void vehicleStore.fetchVehicles())
watch(
  vin,
  (v) => {
    if (!v) return
    if (places.places.length === 0) void places.fetchPlaces(v)
    if (!vehicleStore.currentStatus) void vehicleStore.fetchStatus(v)
    void loadCharges()
  },
  { immediate: true },
)

function insideDraft(c: ChargeSession): boolean {
  if (c.latitude === null || c.longitude === null) return false
  const meters =
    distanceKm([draft.value.latitude, draft.value.longitude], [c.latitude, c.longitude]) * 1000
  return meters <= draft.value.radiusMeters
}

// A hand-set home or public is what the circle would decide; work is never decided by it.
const handSet = computed(() =>
  charges.value.filter((c) => c.place === 'home' || c.place === 'public'),
)
const summary = computed(() => {
  const total = charges.value.length
  if (total === 0) return t('settings.home.noCharges')
  const inside = charges.value.filter(insideDraft).length
  const text = t('settings.home.inside', { inside, total })
  return handSet.value.length ? `${text} ${t('settings.home.handSet', handSet.value.length)}` : text
})

async function resetHandSet() {
  if (!vin.value || resetting.value) return
  const v = vin.value
  error.value = null
  done.value = null
  resetting.value = true
  try {
    const list = handSet.value
    for (const c of list) {
      const updated = await chargesApi.update(v, c.id, {
        notes: c.notes,
        place: null,
        paidAmount: c.paidAmount,
      })
      charges.value = charges.value.map((x) => (x.id === updated.id ? updated : x))
    }
    done.value = t('settings.home.resetDone', list.length)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    resetting.value = false
  }
}
</script>

<template>
  <div class="home-location">
    <p class="settings-toggle__desc">{{ t('settings.home.desc') }}</p>

    <div class="home-location__map">
      <LMap :zoom="16" :center="mapCenter" @ready="onMapReady" @click="onMapClick">
        <LCircle :lat-lng="pin" :radius="draft.radiusMeters" />
        <LCircleMarker
          v-if="carPosition"
          :lat-lng="carPosition"
          :radius="7"
          class-name="home-location__car"
          :interactive="false"
        />
        <LMarker :lat-lng="pin" draggable @update:lat-lng="moveTo" />
      </LMap>
    </div>
    <span class="text-xs text-muted">{{ t('settings.home.mapHint') }}</span>

    <div class="home-location__where">
      <span class="home-location__coords">{{ coordinates }}</span>
      <button
        type="button"
        class="btn btn-primary btn-sm"
        :disabled="!carPosition"
        @click="useCarPosition"
      >
        <font-awesome-icon icon="location-arrow" />{{ t('settings.home.useCar') }}
      </button>
    </div>

    <RangeControl
      v-model="draft.radiusMeters"
      icon="location-dot"
      :label="t('settings.home.radius')"
      :value-text="t('settings.home.meters', { n: draft.radiusMeters })"
      :min="COMMUTE_RADIUS.min"
      :max="COMMUTE_RADIUS.max"
      :step="COMMUTE_RADIUS.step"
      :edge-labels="[
        t('settings.home.meters', { n: COMMUTE_RADIUS.min }),
        t('settings.home.oneKm'),
      ]"
    />
    <p class="text-xs text-muted">{{ t('settings.home.radiusHint') }}</p>

    <div class="home-location__actions">
      <button
        v-if="home && changed"
        type="button"
        class="btn btn-outline-secondary btn-sm"
        :disabled="busy"
        @click="reset"
      >
        {{ t('common.cancel') }}
      </button>
      <button
        type="button"
        class="btn btn-success btn-sm"
        :disabled="busy || !changed || !vin"
        @click="save"
      >
        <font-awesome-icon icon="house" />{{ t('settings.home.save') }}
      </button>
    </div>

    <SettingsToggle
      v-model="uiSettings.autoHomeCharges"
      input-id="settings-toggle-auto-home"
      :label="t('settings.home.auto')"
      :desc="t('settings.home.autoDesc')"
    />

    <div class="home-location__past">
      <span class="settings-card__subtitle">{{ t('settings.home.pastTitle') }}</span>
      <span class="text-sm">{{ summary }}</span>
      <button
        v-if="handSet.length"
        type="button"
        class="btn btn-outline-secondary btn-sm"
        :disabled="resetting"
        @click="resetHandSet"
      >
        {{ t('settings.home.reset') }}
      </button>
    </div>

    <p v-if="done" class="text-sm home-location__done" role="status">{{ done }}</p>
    <p v-if="error" class="text-sm text-danger" role="alert">{{ error }}</p>
    <p class="text-xs text-muted">{{ t('settings.home.sameAsTrips') }}</p>
  </div>
</template>

<style scoped>
.home-location {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.home-location__map {
  height: 260px;
  width: 100%;
  border-radius: var(--radius);
  overflow: hidden;
}

@media (width >= 900px) {
  .home-location__map {
    height: 320px;
  }
}

.home-location__where {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.home-location__coords {
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: 0.85rem;
}

.home-location__where .btn,
.home-location__actions .btn {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  min-height: 2.5rem;
}

.home-location__actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}

.home-location__past {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.4rem;
  padding: 0.75rem;
  border-radius: var(--radius);
  background: var(--color-surface-2);
}

.home-location__done {
  color: var(--color-success);
}

.home-location :deep(.home-location__car) {
  fill: var(--color-success);
  fill-opacity: 1;
  stroke: var(--color-bg);
  stroke-width: 3;
}
</style>
