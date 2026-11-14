<script setup lang="ts">
import '@/assets/logList.css'
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TripLogEntry } from '@/services/tripLogApi'
import type { CommutePlace } from '@/services/commutePlacesApi'
import { formatNumber, formatTime } from '@/utils/format'
import { formatTripDuration } from '@/utils/tripRows'
import {
  loggedDistanceKm,
  odometerDistanceKm,
  PURPOSE_BADGE_CLASSES,
  PURPOSE_ICONS,
} from '@/utils/tripLog'
import { COMMUTE_PLACE_ICONS } from '@/utils/commutePlaces'
import { tripEnergy } from '@/utils/energy'
import { useVehicleStore } from '@/stores/vehicle'
import { useUnits } from '@/composables/useUnits'
import SocBar from './SocBar.vue'
import { useTripEnds } from '@/composables/useTripEnds'

const props = defineProps<{
  entry: TripLogEntry
  /** False when place names are switched off: the ends then read as coordinates. */
  placesShown: boolean
  /** True while addresses are being looked up, so a missing one shows a placeholder. */
  placesResolving: boolean
  saving: boolean
  /** Home and work, to mark the ends that are at one of them. */
  commutePlaces: readonly CommutePlace[]
  /** The trip the detail shows. */
  selected: boolean
}>()

/** Asks for this trip's detail: beside the list, or in a dialog on a narrower screen. */
const emit = defineEmits<{ (e: 'select'): void }>()

const { t } = useI18n()
const units = useUnits()
const vehicleStore = useVehicleStore()

const { from, to } = useTripEnds({
  entry: toRef(props, 'entry'),
  placesShown: toRef(props, 'placesShown'),
  placesResolving: toRef(props, 'placesResolving'),
  commutePlaces: toRef(props, 'commutePlaces'),
})

// A drive saved without a route: when it left is not known, so it shows only when it was found.
const noRoute = computed(() => !!props.entry.leftAfter)
// A drive followed from part-way only: when it left its parking spot is an estimate.
const partRoute = computed(() => !noRoute.value && props.entry.unfollowedKm != null)
const startTime = computed(() => {
  if (noRoute.value) return '?'
  const time = formatTime(props.entry.startedAt)
  return partRoute.value ? `≈${time}` : time
})
const endTime = computed(() => formatTime(props.entry.endedAt))

const durationLabel = computed(() => {
  const duration = formatTripDuration(props.entry.startedAt, props.entry.endedAt, t)
  return partRoute.value ? `≈${duration}` : duration
})

const distanceLabel = computed(() => units.value.format('distance', loggedDistanceKm(props.entry)))
const fromOdometer = computed(() => odometerDistanceKm(props.entry) !== null)
const purposeKey = computed(() => props.entry.purpose ?? 'unclassified')

const energy = computed(() =>
  tripEnergy(
    props.entry,
    loggedDistanceKm(props.entry),
    vehicleStore.effectiveVehicleType,
    vehicleStore.hvBatteryCapacityKwh ?? vehicleStore.currentStatus?.hvTotalCapacityKwh ?? null,
  ),
)
const efficiency = computed(() =>
  energy.value ? units.value.format('energyPerDistance', energy.value.whPerKm) : null,
)

const socLabel = computed(() => {
  const { socStartPercent: start, socEndPercent: end } = props.entry
  if (start == null || end == null) return null
  const delta = Math.round(end - start)
  return `${Math.round(start)}% → ${Math.round(end)}% (${delta > 0 ? '+' : ''}${delta}%)`
})

// The icon's name, with how the purpose was set when it was set automatically.
const purposeTitle = computed(() => {
  const purpose = t(`tripLog.purpose.${purposeKey.value}`)
  return props.entry.purposeIsAutomatic ? `${purpose} (${t('tripLog.places.automatic')})` : purpose
})
</script>

<template>
  <li
    class="trip-log-item ll-item"
    :class="{
      'trip-log-item--saving': saving,
      'trip-log-item--hidden': entry.isHidden,
      'll-item--hidden': entry.isHidden,
      'trip-log-item--selected': selected,
      'trip-log-item--no-route': noRoute || partRoute,
      'll-item--open': selected,
    }"
  >
    <button
      type="button"
      class="trip-log-item__select ll-row"
      :aria-pressed="selected"
      :aria-expanded="selected"
      @click="emit('select')"
    >
      <!-- The purpose at a glance; the detail spells it out. -->
      <span
        class="trip-log-item__icon ll-row__lead"
        :class="PURPOSE_BADGE_CLASSES[purposeKey]"
        role="img"
        :title="purposeTitle"
        :aria-label="purposeTitle"
      >
        <font-awesome-icon :icon="PURPOSE_ICONS[purposeKey]" />
        <span v-if="entry.purposeIsAutomatic" class="trip-log-item__auto">
          <font-awesome-icon icon="wand-magic-sparkles" />
        </span>
      </span>

      <span class="ll-row__main">
        <span class="trip-log-item__when ll-row__time">
          {{ startTime }}<span class="trip-log-item__end-time"> → {{ endTime }}</span>
          <small v-if="!noRoute" class="trip-log-item__duration">{{ durationLabel }}</small>
          <span
            v-else
            class="ll-pill ll-pill--warn trip-log-item__no-route"
            :title="t('tripLog.noRouteHint')"
            >{{ t('tripLog.noRoute') }}</span
          >
          <span
            v-if="partRoute"
            class="ll-pill ll-pill--warn trip-log-item__no-route"
            :title="t('tripLog.partRouteHint')"
            >{{ t('tripLog.partRoute') }}</span
          >
          <font-awesome-icon
            v-if="entry.isEdited"
            icon="pen-to-square"
            class="trip-log-item__mark trip-log-item__edited"
            :title="t('tripLog.editedHint')"
            role="img"
            :aria-label="t('tripLog.edited')"
          />
          <font-awesome-icon
            v-if="entry.notes"
            icon="message"
            class="trip-log-item__mark trip-log-item__note"
            :title="entry.notes"
            role="img"
            :aria-label="entry.notes"
          />
        </span>
        <span class="trip-log-item__route ll-row__place">
          <span
            v-if="from.pending"
            class="skeleton skeleton--text skeleton--text-md"
            :aria-label="t('tripLog.resolvingPlaces')"
          />
          <span v-else class="trip-log-item__place" :title="from.label ?? undefined">
            <font-awesome-icon
              v-if="from.commutePlace"
              :icon="COMMUTE_PLACE_ICONS[from.commutePlace.kind]"
              class="trip-log-item__place-icon"
            />{{ from.label }}
          </span>
          <font-awesome-icon icon="arrow-right" class="trip-log-item__arrow" aria-hidden="true" />
          <span
            v-if="to.pending"
            class="skeleton skeleton--text skeleton--text-md"
            :aria-label="t('tripLog.resolvingPlaces')"
          />
          <span v-else class="trip-log-item__place" :title="to.label ?? undefined">
            <font-awesome-icon
              v-if="to.commutePlace"
              :icon="COMMUTE_PLACE_ICONS[to.commutePlace.kind]"
              class="trip-log-item__place-icon"
            />{{ to.label }}
          </span>
        </span>
      </span>

      <span class="ll-row__soc">
        <SocBar :start="entry.socStartPercent ?? null" :end="entry.socEndPercent ?? null" />
        <span v-if="socLabel">{{ socLabel }}</span>
        <!-- On a phone the first line has no room for it beside the times. -->
        <span
          v-if="noRoute || partRoute"
          class="ll-pill ll-pill--warn trip-log-item__flag"
          :title="noRoute ? t('tripLog.noRouteHint') : t('tripLog.partRouteHint')"
          >{{ noRoute ? t('tripLog.noRoute') : t('tripLog.partRoute') }}</span
        >
      </span>

      <span class="ll-row__figures">
        <span
          class="trip-log-item__km ll-row__big"
          :title="fromOdometer ? t('tripLog.distanceFromOdometer') : t('tripLog.distanceFromGps')"
          >{{ distanceLabel }}</span
        >
        <span v-if="energy" class="ll-row__small"
          >{{ formatNumber(energy.kwh, energy.kwh < 10 ? 2 : 1) }} {{ t('common.kwh') }}</span
        >
        <span v-if="efficiency" class="ll-pill ll-pill--good">{{ efficiency }}</span>
      </span>
    </button>
    <slot />
  </li>
</template>
