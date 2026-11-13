<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { LCircleMarker, LMap, LPolyline } from '@vue-leaflet/vue-leaflet'
import type { Marker } from 'leaflet'
import type { TripPoint } from '@/services/vehicleApi'
import { useLeafletMap } from '@/composables/useLeafletMap'
import { useBasemap } from '@/composables/useBasemap'
import { fitToPoints, L, type FitPadding, type LeafletMap } from '@/utils/leaflet'
import { latLngsOf, type LatLng } from '@/utils/geo'
import { speedSegments, speedToColor } from '@/utils/speedColors'
import { positionAt } from '@/utils/routePosition'

const props = defineProps<{
  /** The route's fixes; null while they load or when they could not be. */
  points: readonly TripPoint[] | null
  loading: boolean
  /** Where the trip started and ended, for a straight line when the fixes are not to be had. */
  start: LatLng
  end: LatLng
  tripId: number
  startedAt: string
  /** A hidden trip is left off the map, so there is nothing there to open. */
  linkToMap: boolean
  /**
   * A drive saved without a route: its two points are where it was parked before and after, so
   * they are drawn as the straight line they are, never as a route.
   */
  noRoute?: boolean
  /**
   * A drive followed from part-way only: the stretch from its parking spot to the first fix the
   * car reported is drawn dashed, as the straight line it is, and the rest as the route.
   */
  partRoute?: boolean
  /**
   * The moment picked on the trip's chart, with what to say about it: the car is shown where it
   * was then, the route still to come greyed out. Null when the chart is not pointed at.
   */
  cursor?: { time: number; label: string } | null
}>()

const { t } = useI18n()

// A preview that cannot be panned or zoomed, as the dashboard's location card.
const mapOptions = {
  zoomControl: false,
  dragging: false,
  scrollWheelZoom: false,
  doubleClickZoom: false,
  touchZoom: false,
  boxZoom: false,
  keyboard: false,
  attributionControl: true,
}

const wrapper = ref<HTMLElement | null>(null)
const { mapInstance, bindMapReady } = useLeafletMap(wrapper)
// Raster tiles: the same streets without pulling the vector renderer into the trip log.
useBasemap(mapInstance, { vector: false })

// The fixes the car reported no speed for are drawn in the line colour of the map's trips.
const NO_SPEED_COLOR = '#3b82f6'

// Of a part-route trip only the fixes from the first one the car reported itself are a route.
const followedFrom = computed(() => {
  if (!props.partRoute || !props.points) return 0
  const first = props.points.findIndex((p) => p.source == null)
  return first > 0 ? first : 0
})

const segments = computed(() => {
  if (props.noRoute || !props.points) return []
  const followed = props.points.slice(followedFrom.value)
  if (followed.length < 2) return []
  return speedSegments({ points: followed }, null).map((segment) => ({
    coordinates: segment.coordinates,
    color: speedToColor(segment.speed, NO_SPEED_COLOR),
  }))
})

// The stretch nobody followed: from the parking spot to where the car was first seen driving.
const unfollowed = computed<LatLng[] | null>(() =>
  followedFrom.value > 0 && props.points
    ? latLngsOf(props.points.slice(0, followedFrom.value + 1))
    : null,
)

// Without fixes, once they stopped loading, the ends joined by a straight line still say where
// the trip went.
const straightLine = computed<LatLng[] | null>(() =>
  !props.loading && segments.value.length === 0 ? [props.start, props.end] : null,
)

const framed = computed<LatLng[]>(() =>
  props.points && props.points.length > 0 ? latLngsOf(props.points) : [props.start, props.end],
)

// Both ends stay in view and out from under what lies over the map: the full map button in the
// top right corner (0.5rem in, 2.1rem tall) and the attribution along the bottom, with room for
// the whole of the car's marker when it is at either end.
const ROUTE_PADDING: FitPadding = { topLeft: [36, 56], bottomRight: [36, 48] }

function frame(map: LeafletMap) {
  fitToPoints(map, framed.value, ROUTE_PADDING)
}

// The panel's width can change after the first frame, as when the speed chart arrives and gives
// it a scrollbar. Leaflet keeps the centre on a resize, which can push an end off the edge, so
// the route is framed again instead.
function onMapReady(map: LeafletMap) {
  bindMapReady(map, (ready) => {
    frame(ready)
    ready.on('resize', () => frame(ready))
  })
}

watch(framed, () => {
  if (mapInstance.value) frame(mapInstance.value)
})

// Where the car was at the chart's moment, read off the fixes; a trip without a route has none.
const position = computed(() =>
  props.cursor && !props.noRoute && props.points
    ? positionAt(props.points, props.cursor.time)
    : null,
)

// The car is one marker moved about, not one made anew for each step of the pointer.
let car: Marker | null = null
const CAR_HTML =
  '<span class="trip-route-map__car-dot"><svg viewBox="-12 -12 24 24" aria-hidden="true">' +
  '<path d="M0 -6.5 L5.5 5.5 L0 2.5 L-5.5 5.5 Z" /></svg></span>' +
  '<span class="trip-route-map__car-label"></span>'
// Room the label takes beside the marker, to put it on the other side near an edge.
const LABEL_ROOM = { x: 150, y: 44 }

watch(
  [mapInstance, position, () => props.cursor?.label],
  ([map, pos, label]) => {
    if (!map || !pos) {
      car?.remove()
      car = null
      return
    }
    if (!car) {
      car = L.marker(pos.at, {
        icon: L.divIcon({ className: 'trip-route-map__car', html: CAR_HTML, iconSize: [0, 0] }),
        interactive: false,
        keyboard: false,
        zIndexOffset: 1000,
      }).addTo(map)
    } else {
      car.setLatLng(pos.at)
    }
    const el = car.getElement()
    if (!el) return
    const dot = el.querySelector<HTMLElement>('.trip-route-map__car-dot')
    if (dot) {
      dot.style.setProperty('--heading', `${pos.heading ?? 0}deg`)
      dot.classList.toggle('trip-route-map__car-dot--still', pos.heading === null)
    }
    const text = el.querySelector<HTMLElement>('.trip-route-map__car-label')
    if (text) text.textContent = label ?? ''
    const point = map.latLngToContainerPoint(pos.at)
    const size = map.getSize()
    el.classList.toggle('trip-route-map__car--left', point.x > size.x - LABEL_ROOM.x)
    el.classList.toggle('trip-route-map__car--below', point.y < LABEL_ROOM.y)
  },
  { flush: 'post' },
)

onUnmounted(() => {
  car?.remove()
  car = null
})
</script>

<template>
  <div ref="wrapper" class="trip-route-map" :aria-label="t('tripLog.detail.routePreview')">
    <LMap
      :zoom="13"
      :center="start"
      :options="mapOptions"
      class="trip-route-map__canvas"
      @ready="onMapReady"
    >
      <LPolyline
        v-for="(segment, i) in segments"
        :key="`segment-${i}`"
        :lat-lngs="segment.coordinates"
        :color="segment.color"
        :weight="5"
        :opacity="0.9"
        :interactive="false"
      />
      <LPolyline
        v-if="straightLine"
        :lat-lngs="straightLine"
        :class-name="
          noRoute
            ? 'trip-route-map__straight trip-route-map__straight--no-route'
            : 'trip-route-map__straight'
        "
        :weight="3"
        dash-array="6 8"
        :interactive="false"
      />
      <!-- The route still to come from where the car is on the chart's moment. -->
      <LPolyline
        v-if="position && position.ahead.length > 1"
        :lat-lngs="position.ahead"
        class-name="trip-route-map__ahead"
        :weight="5"
        :opacity="1"
        :interactive="false"
      />
      <template v-if="unfollowed">
        <LPolyline
          :lat-lngs="unfollowed"
          class-name="trip-route-map__straight trip-route-map__straight--no-route"
          :weight="3"
          dash-array="6 8"
          :interactive="false"
        />
        <LCircleMarker
          :lat-lng="unfollowed[unfollowed.length - 1]!"
          :radius="4"
          class-name="trip-route-map__end trip-route-map__end--followed"
          :interactive="false"
        />
      </template>
      <LCircleMarker
        :lat-lng="start"
        :radius="6"
        class-name="trip-route-map__end trip-route-map__end--start"
        :interactive="false"
      />
      <LCircleMarker
        :lat-lng="end"
        :radius="6"
        class-name="trip-route-map__end trip-route-map__end--finish"
        :interactive="false"
      />
    </LMap>

    <span
      v-if="loading"
      class="trip-route-map__loading skeleton"
      role="status"
      :aria-label="t('tripEdit.loading')"
    />

    <RouterLink
      v-if="linkToMap"
      class="trip-route-map__open"
      :to="{ name: 'map', query: { trip: tripId, at: startedAt } }"
      :aria-label="t('tripLog.showOnMap')"
      :title="t('tripLog.showOnMap')"
    >
      <font-awesome-icon icon="map" />
    </RouterLink>
  </div>
</template>
