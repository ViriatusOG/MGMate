<script setup lang="ts">
import '@/assets/carDiagram.css'
import { useI18n } from 'vue-i18n'
import { computed, nextTick, ref, shallowRef } from 'vue'
import CardInfoWrap from './CardInfoWrap.vue'
import CarLightBeams from './carDiagram/CarLightBeams.vue'
import LevelBadge from './carDiagram/LevelBadge.vue'
import SpeedGauge from './carDiagram/SpeedGauge.vue'
import TyreIndicators from './carDiagram/TyreIndicators.vue'
import TyreLabels from './carDiagram/TyreLabels.vue'
import CarStatusList from './overview/CarStatusList.vue'
import Mg4TopView from './overview/Mg4TopView.vue'
import { useCarStatus } from './overview/useCarStatus'
import type { LevelKind } from '@/composables/useStatisticsCharts'
import { useTyrePressureThresholds } from '@/composables/useTyrePressureThresholds'
import { useUnits } from '@/composables/useUnits'
import { evLevelVariant, fuelLevelVariant, type LevelVariant } from '@/utils/levels'
import { useVehicleStore } from '@/stores/vehicle'
import { useVehiclePhoto } from '@/composables/useVehiclePhoto'
import { useErrorMessage } from '@/composables/useErrorMessage'
import { ApiError } from '@/services/apiCore'
import { useTyreDisplay, type TyrePosition } from './carDiagram/useTyreDisplay'

const { t } = useI18n()
const tyreThresholds = useTyrePressureThresholds()
const units = useUnits()

// Tapping a tyre's pressure opens its history, and tapping the fuel or battery level its own.
// The level stays set after closing, so the dialog keeps its title while it fades out.
const tyreHistoryOpen = ref(false)
const levelHistoryOpen = ref(false)
const levelHistory = ref<LevelKind>('fuel')

// The dialogs chart with Chart.js, which is as large as the rest of the dashboard together, so
// each is fetched on its first tap rather than with the page. It is mounted closed and opened on
// the next tick: a dialog mounted already open would skip its fade-in and the fetch its watcher
// starts when it opens.
const TyreHistoryModal = shallowRef<typeof import('./TyreHistoryModal.vue').default | null>(null)
const LevelHistoryModal = shallowRef<typeof import('./LevelHistoryModal.vue').default | null>(null)

async function openTyreHistory() {
  TyreHistoryModal.value ??= (await import('./TyreHistoryModal.vue')).default
  await nextTick()
  tyreHistoryOpen.value = true
}

async function openLevelHistory(level: LevelKind) {
  LevelHistoryModal.value ??= (await import('./LevelHistoryModal.vue')).default
  levelHistory.value = level
  await nextTick()
  levelHistoryOpen.value = true
}

const props = defineProps<{
  frontLeft: number | null
  frontRight: number | null
  rearLeft: number | null
  rearRight: number | null
  driverDoorOpen?: boolean | null
  passengerDoorOpen?: boolean | null
  rearLeftDoorOpen?: boolean | null
  rearRightDoorOpen?: boolean | null
  trunkOpen?: boolean | null
  bonnetOpen?: boolean | null
  lightsMainBeam?: boolean | null
  lightsDippedBeam?: boolean | null
  lightsSide?: boolean | null
  evSocPercent?: number | null
  fuelLevelPercent?: number | null
  chargerConnected?: boolean | null
  isCharging?: boolean | null
  speed?: number | null
  /** Whether the overview shows its controls row, which the header link scrolls to. */
  hasControls?: boolean
}>()
const emit = defineEmits<{ controls: [] }>()

// Locks, doors, windows, lids, lights and the cable, in words under the drawing or the photo.
const carStatus = useCarStatus()
const statusRows = carStatus.compactRows
const locked = carStatus.locked

const tyrePressures = computed(() => ({
  frontLeft: props.frontLeft,
  frontRight: props.frontRight,
  rearLeft: props.rearLeft,
  rearRight: props.rearRight,
}))

// The colour bands, in the pressure unit this browser shows: the thresholds themselves are
// configured in bar, which is what the car reports.
const tyreLegendParams = computed(() => {
  const u = units.value
  const value = (bar: number) => u.measure('pressure', bar)!.value
  return {
    low: value(tyreThresholds.value.lowBar),
    good: value(tyreThresholds.value.goodBar),
    high: value(tyreThresholds.value.highBar),
    unit: u.symbol('pressure'),
  }
})

const doorBadges = computed(() => [
  {
    key: 'bonnet',
    open: props.bonnetOpen,
    suffix: 'bonnet',
    titleKey: 'vehicle.doors_detail.bonnet',
  },
  {
    key: 'driver',
    open: props.driverDoorOpen,
    suffix: 'door-fl',
    titleKey: 'vehicle.doors_detail.driver',
  },
  {
    key: 'passenger',
    open: props.passengerDoorOpen,
    suffix: 'door-fr',
    titleKey: 'vehicle.doors_detail.passenger',
  },
  {
    key: 'rearLeft',
    open: props.rearLeftDoorOpen,
    suffix: 'door-rl',
    titleKey: 'vehicle.doors_detail.rearLeft',
  },
  {
    key: 'rearRight',
    open: props.rearRightDoorOpen,
    suffix: 'door-rr',
    titleKey: 'vehicle.doors_detail.rearRight',
  },
  { key: 'trunk', open: props.trunkOpen, suffix: 'trunk', titleKey: 'vehicle.doors_detail.boot' },
])

const hasLights = computed(() => props.lightsMainBeam || props.lightsDippedBeam || props.lightsSide)

// A level to show on the diagram, or null when the car reports none. The same bands as the
// dashboard's fuel and battery cards, so both colour a level alike.
function level(
  percent: number | null | undefined,
  variantOf: (pct: number | null) => LevelVariant | undefined,
): { percent: number; variant: LevelVariant } | null {
  const variant = variantOf(percent ?? null)
  return percent != null && variant ? { percent, variant } : null
}

const fuelLevel = computed(() => level(props.fuelLevelPercent, fuelLevelVariant))
const batteryLevel = computed(() => level(props.evSocPercent, evLevelVariant))

const isMoving = computed(() => (props.speed ?? 0) > 0)

// The owner's own photo of the car, shown in place of the drawn top view when there is one.
const vehicleStore = useVehicleStore()
const photo = useVehiclePhoto(computed(() => vehicleStore.activeVin))
const errorMessage = useErrorMessage()
const photoInput = ref<HTMLInputElement | null>(null)
const tyreDisplay = useTyreDisplay()

// Why an upload failed, said plainly for the two reasons a picked file is refused.
const photoError = computed(() => {
  const e = photo.error.value
  if (e instanceof ApiError && e.status === 413) return t('vehicle.photo.tooLarge')
  if (e instanceof ApiError && e.code === 'vehiclePhoto.unsupported')
    return t('vehicle.photo.unsupported')
  return e ? errorMessage(e) : null
})

function pickPhoto() {
  photoInput.value?.click()
}

async function onPhotoPicked(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) await photo.upload(file)
}

function removePhoto() {
  if (window.confirm(t('vehicle.photo.removeConfirm'))) photo.remove()
}

// Parked, the photo; on the move, the drawing with its road and speed.
const showPhoto = computed(() => photo.url.value !== null && !isMoving.value)

const TYRE_TILES: TyrePosition[] = ['frontLeft', 'frontRight', 'rearLeft', 'rearRight']

// What the drawing shows by where it sits on the car, said in words beside the photo instead.
const photoChips = computed(() => {
  const chips: { key: string; icon: string; text: string; variant: string }[] = []
  if (props.isCharging)
    chips.push({ key: 'charging', icon: 'bolt', text: t('vehicle.chargingYes'), variant: 'good' })
  else if (props.chargerConnected)
    chips.push({
      key: 'plugged',
      icon: 'plug',
      text: t('vehicle.hvBattery.pluggedIn'),
      variant: 'info',
    })
  for (const badge of doorBadges.value) {
    if (badge.open)
      chips.push({ key: badge.key, icon: 'lock-open', text: t(badge.titleKey), variant: 'warn' })
  }
  if (hasLights.value)
    chips.push({
      key: 'lights',
      icon: 'lightbulb',
      text: t('vehicle.photo.lightsOn'),
      variant: 'info',
    })
  return chips
})

const roadAnimDuration = computed(() => {
  const s = props.speed ?? 0
  if (s <= 0) return '2s'
  return `${Math.max(0.1, Math.min(2, 30 / s)).toFixed(2)}s`
})

const motionBlurAmount = computed(() => {
  const s = props.speed ?? 0
  if (s <= 100) return 0
  return Math.min(10, (s - 100) / 5)
})
</script>

<template>
  <CardInfoWrap :title="t('vehicle.overview')">
    <template #info>
      <div class="card-info-sections">
        <div class="card-info-section">
          <p class="card-info-section__title">
            <font-awesome-icon icon="circle" class="tyre-legend-dot tyre-legend-dot--ok" />
            <font-awesome-icon icon="circle" class="tyre-legend-dot tyre-legend-dot--warning" />
            <font-awesome-icon icon="circle" class="tyre-legend-dot tyre-legend-dot--danger" />
            {{ t('vehicle.diagram.infoTyreTitle') }}
          </p>
          <p class="card-info-desc">
            {{ t('vehicle.diagram.infoTyreDesc', tyreLegendParams) }}
          </p>
        </div>
        <div class="card-info-section">
          <p class="card-info-section__title">
            <font-awesome-icon icon="lock-open" />
            {{ t('vehicle.diagram.infoDoorsTitle') }}
          </p>
          <p class="card-info-desc">{{ t('vehicle.diagram.infoDoorsDesc') }}</p>
        </div>
        <div class="card-info-section">
          <p class="card-info-section__title">
            <font-awesome-icon icon="lightbulb" />
            {{ t('vehicle.diagram.infoLightsTitle') }}
          </p>
          <p class="card-info-desc">{{ t('vehicle.diagram.infoLightsDesc') }}</p>
        </div>
        <div class="card-info-section">
          <p class="card-info-section__title">
            <font-awesome-icon icon="gas-pump" />
            {{ t('vehicle.diagram.infoLevelTitle') }}
          </p>
          <p class="card-info-desc">{{ t('vehicle.diagram.infoLevelDesc') }}</p>
        </div>
        <div class="card-info-section">
          <p class="card-info-section__title">
            <font-awesome-icon icon="plug" />
            {{ t('vehicle.diagram.infoChargingTitle') }}
          </p>
          <p class="card-info-desc">{{ t('vehicle.diagram.infoChargingDesc') }}</p>
        </div>
      </div>
    </template>
    <div class="tyre-diagram">
      <div class="car-photo__title-row">
        <p class="card-title">
          <font-awesome-icon icon="car-side" />
          {{ t('vehicle.overview') }}
        </p>
        <span class="car-photo__actions">
          <button
            v-if="hasControls"
            type="button"
            class="ov-card__link ov-status__link"
            @click="emit('controls')"
          >
            {{ t('overview.status.controls') }}
          </button>
          <button
            type="button"
            class="btn btn-sm btn-outline-secondary car-photo__button"
            :disabled="photo.busy.value"
            :title="photo.url.value ? t('vehicle.photo.change') : t('vehicle.photo.add')"
            :aria-label="photo.url.value ? t('vehicle.photo.change') : t('vehicle.photo.add')"
            @click="pickPhoto"
          >
            <font-awesome-icon icon="camera" />
          </button>
          <button
            v-if="photo.url.value"
            type="button"
            class="btn btn-sm btn-outline-secondary car-photo__button"
            :disabled="photo.busy.value"
            :title="t('vehicle.photo.remove')"
            :aria-label="t('vehicle.photo.remove')"
            @click="removePhoto"
          >
            <font-awesome-icon icon="trash" />
          </button>
          <input
            ref="photoInput"
            type="file"
            accept="image/png,image/jpeg,image/webp"
            class="car-photo__input"
            @change="onPhotoPicked"
          />
        </span>
      </div>
      <p v-if="photo.error.value" class="text-xs text-danger" role="alert">
        {{ photoError }}
      </p>

      <div v-if="showPhoto" class="car-photo">
        <div class="car-photo__chips">
          <span
            v-for="chip in photoChips"
            :key="chip.key"
            class="car-photo__chip"
            :class="`car-photo__chip--${chip.variant}`"
          >
            <font-awesome-icon :icon="chip.icon" />{{ chip.text }}
          </span>
        </div>
        <img
          :src="photo.url.value ?? undefined"
          :alt="t('vehicle.photo.alt')"
          class="car-photo__img"
        />
        <div class="car-photo__levels">
          <LevelBadge
            v-if="batteryLevel"
            class="diagram-level--inline"
            :class="{
              'diagram-level--charging': isCharging,
              'diagram-level--connected': chargerConnected && !isCharging,
            }"
            icon="bolt"
            :label="t('vehicle.evSoc')"
            :percent="batteryLevel.percent"
            :variant="batteryLevel.variant"
            @select="openLevelHistory('battery')"
          />
          <LevelBadge
            v-if="fuelLevel"
            class="diagram-level--inline"
            icon="gas-pump"
            :label="t('vehicle.fuel')"
            :percent="fuelLevel.percent"
            :variant="fuelLevel.variant"
            @select="openLevelHistory('fuel')"
          />
        </div>
        <div class="car-photo__tyres">
          <button
            v-for="position in TYRE_TILES"
            :key="position"
            type="button"
            class="car-photo__tyre"
            :class="`car-photo__tyre--${tyreDisplay.variant(tyrePressures[position])}`"
            :aria-label="
              t('tyreHistory.open', {
                position: t(`vehicle.diagram.tyrePosition.${position}`),
                reading: tyreDisplay.reading(tyrePressures[position]),
              })
            "
            @click="openTyreHistory()"
          >
            <span class="car-photo__tyre-label">{{
              t(`vehicle.diagram.tyrePosition.${position}`)
            }}</span>
            <strong>{{ tyreDisplay.reading(tyrePressures[position]) }}</strong>
          </button>
        </div>
      </div>

      <div v-else class="tyre-diagram__wrap">
        <div class="tyre-car-wrap">
          <!--
          ViewBox: -40 0 420 480  (widened by 40 px each side; car stays at x=170 = 50%)
          Orange car: nested SVG, original bounds x≈137–245 y≈237–445
            placed at x=105 y=115 width=130 height=250 in parent coords.
          Car body parent coords: x≈119–222; center x=170.
          Wheel corners (parent): FL(127,123) FR(213,123) RL(127,349) RR(213,349)
          Dots at outer side of each wheel: FL(110,145) FR(230,145) RL(110,327) RR(230,327)
          Labels at (~20° from horizontal):  FL→(72,131) FR→(268,131) RL→(72,341) RR→(268,341)
          CSS % = (svg_x + 40) / 420: FL/RL=26.7%  FR/RR=73.3%
          Side light cones: left x=72–118 y=137–147 / right x=222–268 y=137–147
          Door icon badges: see .car-badge-* CSS classes
        -->
          <svg
            viewBox="-40 0 420 480"
            xmlns="http://www.w3.org/2000/svg"
            class="tyre-diagram__svg"
            :aria-label="t('vehicle.diagramLabel')"
          >
            <defs>
              <!-- Motion blur above 100 km/h: ghost is shifted toward the rear (downward)
                   and blurred, then the sharp original is composited on top so the
                   blur trails behind the car rather than spreading symmetrically. -->
              <filter id="motion-blur" x="-5%" y="-5%" width="110%" height="160%">
                <feOffset in="SourceGraphic" :dy="motionBlurAmount * 2" result="shifted" />
                <feGaussianBlur
                  in="shifted"
                  :stdDeviation="`0 ${motionBlurAmount}`"
                  result="blurred"
                />
                <feMerge>
                  <feMergeNode in="blurred" />
                  <feMergeNode in="SourceGraphic" />
                </feMerge>
              </filter>
              <!-- A soft shadow under the car, so a pale paint (white, silver) stands off the
                   white card and a dark one off the road. -->
              <filter id="car-shadow" x="-20%" y="-10%" width="140%" height="120%">
                <feDropShadow dx="0" dy="4" stdDeviation="5" class="car-shadow" />
              </filter>
              <!-- The road fades out at the top and bottom instead of ending on a hard edge. -->
              <linearGradient id="road-fade-grad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" stop-color="#fff" stop-opacity="0" />
                <stop offset="0.14" stop-color="#fff" />
                <stop offset="0.86" stop-color="#fff" />
                <stop offset="1" stop-color="#fff" stop-opacity="0" />
              </linearGradient>
              <mask id="road-fade" maskContentUnits="userSpaceOnUse">
                <rect x="-40" y="0" width="420" height="480" fill="url(#road-fade-grad)" />
              </mask>
            </defs>

            <!-- Road animation: visible only while driving (speed > 0).
                 ViewBox is -40 0 420 480. 3 equal lanes of 140 px each.
                 Left lane: x=-40–100. Center lane: x=100–240 (car body x≈119–222, ~20 px margin).
                 Right lane: x=240–380. Edges at x=-30 and x=370. -->
            <g v-if="isMoving" mask="url(#road-fade)">
              <rect x="-40" y="0" width="420" height="480" class="road-surface" />
              <line x1="-30" y1="0" x2="-30" y2="480" class="road-edge" />
              <line x1="370" y1="0" x2="370" y2="480" class="road-edge" />
              <line
                x1="100"
                y1="0"
                x2="100"
                y2="480"
                class="road-center-dash"
                :style="{ animationDuration: roadAnimDuration }"
              />
              <line
                x1="240"
                y1="0"
                x2="240"
                y2="480"
                class="road-center-dash"
                :style="{ animationDuration: roadAnimDuration }"
              />
            </g>

            <!-- Light beams drawn behind the car, which masks where they start. -->
            <CarLightBeams
              v-if="hasLights"
              :main-beam="!!lightsMainBeam"
              :dipped-beam="!!lightsDippedBeam"
            />

            <!-- The MG4 traced from above, in the car colour from Settings. Its 250 × 400 view
                 box is scaled by 0.791 and placed so the axles (y 87 and 317 on its canvas) land
                 on the tyre dots at y 145 and 327, and the body (x 22-178) between x 108 and 232. -->
            <g filter="url(#car-shadow)" class="tyre-diagram__car">
              <g :filter="motionBlurAmount > 0 ? 'url(#motion-blur)' : undefined">
                <Mg4TopView x="71.1" y="76.2" width="197.8" height="316.4" />
              </g>
            </g>

            <!-- Charging cable: from the charger off-screen below to the car's one charge port,
                 on the rear left, which the traced drawing lights up itself (the port is at
                 x 26.8, y 308.75 on its canvas, so 112, 320 here). The path runs from the charger
                 to the car so the dashes flow toward the car. -->
            <g
              v-if="chargerConnected || isCharging"
              class="charge-group"
              :class="{ 'charge-group--active': isCharging }"
            >
              <title>
                {{ isCharging ? t('vehicle.chargingYes') : t('vehicle.hvBattery.pluggedIn') }}
              </title>
              <circle cx="112" cy="320" r="11" class="charge-entry-glow" />
              <path d="M 62,480 C 62,400 70,330 108,321" class="charge-cable" />
            </g>

            <TyreIndicators :pressures="tyrePressures" />
          </svg>

          <TyreLabels :pressures="tyrePressures" @select="openTyreHistory" />

          <!-- The lock sits on the roof; open doors and lids show on the drawing itself. -->
          <span
            v-if="locked !== null"
            class="ov-status__lock tyre-diagram__lock"
            :class="locked ? 'is-locked' : 'is-unlocked'"
            :title="locked ? t('overview.status.locked') : t('overview.status.unlocked')"
          >
            <font-awesome-icon :icon="locked ? 'lock' : 'lock-open'" />
          </span>

          <!-- Fuel level (front) -->
          <LevelBadge
            v-if="fuelLevel"
            class="diagram-level--fuel"
            icon="gas-pump"
            :label="t('vehicle.fuel')"
            :percent="fuelLevel.percent"
            :variant="fuelLevel.variant"
            @select="openLevelHistory('fuel')"
          />

          <!-- EV battery (rear) -->
          <LevelBadge
            v-if="batteryLevel"
            class="diagram-level--battery"
            :class="{
              'diagram-level--charging': isCharging,
              'diagram-level--connected': chargerConnected && !isCharging,
            }"
            icon="bolt"
            :label="t('vehicle.evSoc')"
            :percent="batteryLevel.percent"
            :variant="batteryLevel.variant"
            @select="openLevelHistory('battery')"
          />

          <SpeedGauge v-if="isMoving" :speed-kmh="speed ?? 0" />
        </div>
      </div>

      <CarStatusList
        v-if="statusRows.length"
        class="tyre-diagram__status"
        :rows="statusRows"
        :columns="2"
      />
    </div>
    <component
      :is="TyreHistoryModal"
      v-if="TyreHistoryModal"
      :open="tyreHistoryOpen"
      @close="tyreHistoryOpen = false"
    />
    <component
      :is="LevelHistoryModal"
      v-if="LevelHistoryModal"
      :open="levelHistoryOpen"
      :level="levelHistory"
      @close="levelHistoryOpen = false"
    />
  </CardInfoWrap>
</template>
