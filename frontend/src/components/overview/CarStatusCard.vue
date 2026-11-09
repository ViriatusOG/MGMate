<script setup lang="ts">
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { regionIsRightHandDrive } from '@/utils/region'

// The MG4 from above: an open door swings out, and an open window, boot, bonnet or sunroof turns
// amber. The list beside it says the same in words. The charge port
// sits on the rear left, where the MG4 has it.
defineProps<{
  /** Whether the overview shows its controls row, which the link scrolls to. */
  hasControls?: boolean
}>()
const emit = defineEmits<{ controls: [] }>()

const { t } = useI18n()
const store = useVehicleStore()
const uiSettings = useUiSettingsStore()

const s = computed(() => store.currentStatus)

// The car reports driver and passenger sides; which of them is on the left depends on where the
// wheel is, which follows the region (a British car has it on the right).
const rhd = computed(() => regionIsRightHandDrive(uiSettings.effectiveRegion))

type Corner = 'fl' | 'fr' | 'rl' | 'rr'

const doors = computed((): Record<Corner, boolean | null> => {
  const x = s.value
  const driver = x?.driverDoorOpen ?? null
  const passenger = x?.passengerDoorOpen ?? null
  return {
    fl: rhd.value ? passenger : driver,
    fr: rhd.value ? driver : passenger,
    rl: x?.rearLeftDoorOpen ?? null,
    rr: x?.rearRightDoorOpen ?? null,
  }
})

const windows = computed((): Record<Corner, boolean | null> => {
  const x = s.value
  const driver = x?.driverWindowOpen ?? null
  const passenger = x?.passengerWindowOpen ?? null
  return {
    fl: rhd.value ? passenger : driver,
    fr: rhd.value ? driver : passenger,
    rl: x?.rearLeftWindowOpen ?? null,
    rr: x?.rearRightWindowOpen ?? null,
  }
})

const CORNER_NAME: Record<Corner, string> = {
  fl: 'frontLeft',
  fr: 'frontRight',
  rl: 'rearLeft',
  rr: 'rearRight',
}

function cornerLabel(c: Corner): string {
  if (c === 'fl' || c === 'fr') {
    const driverSide = rhd.value ? 'fr' : 'fl'
    return t(c === driverSide ? 'overview.status.driver' : 'overview.status.passenger')
  }
  return t(`overview.status.${CORNER_NAME[c]}`)
}

const CORNERS: Corner[] = ['fl', 'fr', 'rl', 'rr']

function openList(map: Record<Corner, boolean | null>) {
  const open = CORNERS.filter((c) => map[c] === true)
  const known = CORNERS.some((c) => map[c] !== null)
  return { open, known }
}

type Tone = 'ok' | 'warn' | 'info' | 'muted'
interface Row {
  key: string
  label: string
  value: string
  tone: Tone
}

function openRow(key: string, label: string, map: Record<Corner, boolean | null>): Row | null {
  const { open, known } = openList(map)
  if (!known) return null
  const value =
    open.length === 0
      ? t('overview.status.closed')
      : open.length === 1
        ? `${cornerLabel(open[0]!)} ${t('overview.status.open').toLowerCase()}`
        : t('overview.status.nOpen', { n: open.length })
  return { key, label, value, tone: open.length ? 'warn' : 'ok' }
}

function panelRow(key: string, label: string, open: boolean | null | undefined): Row | null {
  if (open == null) return null
  return {
    key,
    label,
    value: open ? t('overview.status.open') : t('overview.status.closed'),
    tone: open ? 'warn' : 'ok',
  }
}

const lightsOn = computed(() => {
  const x = s.value
  if (!x) return null
  const all = [x.lightsMainBeam, x.lightsDippedBeam, x.lightsSide]
  if (all.every((v) => v == null)) return null
  return all.some((v) => v === true)
})

const cable = computed((): 'charging' | 'plugged' | 'none' | null => {
  const x = s.value
  if (!x) return null
  if (x.isCharging) return 'charging'
  if (x.chargerConnected) return 'plugged'
  return x.chargerConnected === false ? 'none' : null
})

const rows = computed((): Row[] => {
  const x = s.value
  if (!x) return []
  const list: (Row | null)[] = [
    x.isLocked == null
      ? null
      : {
          key: 'locks',
          label: t('overview.status.locks'),
          value: x.isLocked ? t('overview.status.locked') : t('overview.status.unlocked'),
          tone: x.isLocked ? 'ok' : 'warn',
        },
    openRow('doors', t('overview.status.doors'), doors.value),
    openRow('windows', t('overview.status.windows'), windows.value),
    panelRow('boot', t('overview.status.boot'), x.trunkOpen),
    panelRow('bonnet', t('overview.status.bonnet'), x.bonnetOpen),
    panelRow('sunroof', t('overview.status.sunroof'), x.sunRoofOpen),
    lightsOn.value == null
      ? null
      : {
          key: 'lights',
          label: t('overview.status.lights'),
          value: lightsOn.value ? t('overview.status.on') : t('overview.status.off'),
          tone: lightsOn.value ? 'info' : 'muted',
        },
    cable.value == null
      ? null
      : {
          key: 'cable',
          label: t('overview.status.cable'),
          value:
            cable.value === 'charging'
              ? t('overview.status.cableCharging')
              : cable.value === 'plugged'
                ? t('overview.status.cablePlugged')
                : t('overview.status.cableNone'),
          tone: cable.value === 'none' ? 'muted' : cable.value === 'charging' ? 'ok' : 'info',
        },
  ]
  return list.filter((r): r is Row => r !== null)
})

const locked = computed(() => s.value?.isLocked ?? null)

// The drawing is traced from a photo of an MG4 from above, front at the top, on a 200 × 400
// canvas. The view box leaves room either side for the mirrors and an open door.
// Door panels sit on the strip of body beside the roof; the hinge is the front edge.
const DOOR_SHAPES: Record<Corner, { x: number; y: number; w: number; h: number }> = {
  fl: { x: 23.8, y: 140.6, w: 13.2, h: 84 },
  rl: { x: 23.8, y: 227, w: 13.2, h: 70.8 },
  fr: { x: 163, y: 140.6, w: 13.2, h: 84 },
  rr: { x: 163, y: 227, w: 13.2, h: 70.8 },
}
// The side windows show as a thin strip along the edge of the roof.
const WINDOW_SHAPES: Record<Corner, { x: number; y: number; w: number; h: number }> = {
  fl: { x: 36.4, y: 145.4, w: 3.6, h: 74.4 },
  rl: { x: 36.4, y: 229.4, w: 3.6, h: 62.4 },
  fr: { x: 160, y: 145.4, w: 3.6, h: 74.4 },
  rr: { x: 160, y: 229.4, w: 3.6, h: 62.4 },
}

const CAR = {
  body: 'M 100.0 16.4 C 76.6 16.4 64.6 18.2 56.8 23.0 C 46.6 29.0 38.2 36.2 32.2 47.0 C 26.8 56.6 23.8 67.4 23.2 78.2 L 22.6 101.0 C 23.8 104.6 25.0 108.2 25.0 114.2 L 25.0 126.2 C 25.0 135.2 23.2 144.2 23.2 156.2 L 22.6 312.2 C 23.2 327.2 27.4 339.2 29.8 351.2 C 31.6 361.4 37.0 367.4 46.6 371.6 C 58.6 377.0 76.6 380.6 100.0 381.2 C 123.4 380.6 141.4 377.0 153.4 371.6 C 163.0 367.4 168.4 361.4 170.2 351.2 C 172.6 339.2 176.8 327.2 177.4 312.2 L 176.8 156.2 C 176.8 144.2 175.0 135.2 175.0 126.2 L 175.0 114.2 C 175.0 108.2 176.2 104.6 177.4 101.0 L 176.8 78.2 C 176.2 67.4 173.2 56.6 167.8 47.0 C 161.8 36.2 153.4 29.0 143.2 23.0 C 135.4 18.2 123.4 16.4 100.0 16.4 Z',
  mirrors: ['M 25.0 131.0 C 17.8 131.6 11.8 135.2 8.8 140.0 C 7.0 144.2 10.6 146.6 15.4 145.4 L 25.0 144.2 Z ', 'M 175.0 131.0 C 182.2 131.6 188.2 135.2 191.2 140.0 C 193.0 144.2 189.4 146.6 184.6 145.4 L 175.0 144.2 Z'],
  lamps: ['M 57.4 25.4 C 46.6 30.2 39.4 37.4 34.6 45.8 L 37.0 47.0 C 41.8 39.2 49.0 32.6 58.6 27.8 Z', 'M 142.6 25.4 C 153.4 30.2 160.6 37.4 165.4 45.8 L 163.0 47.0 C 158.2 39.2 151.0 32.6 141.4 27.8 Z'],
  bonnet: 'M 100.0 19.4 C 76.6 19.4 61.6 21.8 53.8 26.6 C 43.0 33.8 35.8 43.4 32.2 54.2 C 29.8 66.2 31.6 79.4 35.8 96.2 C 46.6 86.6 70.6 82.4 100.0 81.8 C 129.4 82.4 153.4 86.6 164.2 96.2 C 168.4 79.4 170.2 66.2 167.8 54.2 C 164.2 43.4 157.0 33.8 146.2 26.6 C 138.4 21.8 123.4 19.4 100.0 19.4 Z',
  crease: 'M 62.2 56.6 C 63.4 42.2 67.0 33.8 76.6 30.8 L 123.4 30.8 C 133.0 33.8 136.6 42.2 137.8 56.6',
  roof: 'M 100.0 81.8 C 73.6 81.8 52.6 85.4 40.6 92.6 C 35.8 97.4 35.8 105.2 36.4 114.2 L 37.0 312.2 C 37.6 330.2 40.6 345.2 52.6 353.0 C 64.6 357.8 82.6 359.6 100.0 359.6 C 117.4 359.6 135.4 357.8 147.4 353.0 C 159.4 345.2 162.4 330.2 163.0 312.2 L 163.6 114.2 C 164.2 105.2 164.2 97.4 159.4 92.6 C 147.4 85.4 126.4 81.8 100.0 81.8 Z',
  roofEdge: 'M 38.2 318.8 Q 100.0 324.2 161.8 318.8',
  rear: 'M 100.0 357.2 C 70.6 357.2 46.6 354.2 35.8 343.4 C 29.8 336.2 26.8 329.0 25.0 321.2 C 26.2 336.2 28.6 348.2 31.0 359.0 C 34.6 366.2 40.6 369.8 49.0 372.8 C 61.6 377.6 79.6 380.6 100.0 381.2 C 120.4 380.6 138.4 377.6 151.0 372.8 C 159.4 369.8 165.4 366.2 169.0 359.0 C 171.4 348.2 173.8 336.2 175.0 321.2 C 173.2 329.0 170.2 336.2 164.2 343.4 C 153.4 354.2 129.4 357.2 100.0 357.2 Z',
  spoiler: 'M 100.0 353.0 L 96.4 351.2 C 94.6 341.0 92.2 331.4 88.0 323.0 C 86.2 331.4 83.2 341.0 79.0 348.8 C 67.6 347.6 55.6 343.4 46.6 335.0 C 41.8 329.0 38.8 321.2 37.0 311.0 L 32.2 311.0 C 32.2 327.2 35.8 341.0 46.6 350.0 C 58.6 356.6 76.6 358.4 100.0 358.4 C 123.4 358.4 141.4 356.6 153.4 350.0 C 164.2 341.0 167.8 327.2 167.8 311.0 L 163.0 311.0 C 161.2 321.2 158.2 329.0 153.4 335.0 C 144.4 343.4 132.4 347.6 121.0 348.8 C 116.8 341.0 113.8 331.4 112.0 323.0 C 107.8 331.4 105.4 341.0 103.6 351.2 L 100.0 353.0 Z',
  taillight: 'M 100.0 371.0 L 88.6 367.4 L 50.2 367.4 L 34.6 360.2 L 28.6 343.4 L 30.4 343.4 L 35.8 358.4 L 50.8 365.0 L 88.6 365.0 L 100.0 368.6 L 111.4 365.0 L 149.2 365.0 L 164.2 358.4 L 169.6 343.4 L 171.4 343.4 L 165.4 360.2 L 149.8 367.4 L 111.4 367.4 L 100.0 371.0 Z',
  boot: 'M 100.0 317.0 C 70.6 317.0 46.6 315.2 32.2 311.0 C 28.6 336.2 29.8 357.2 34.6 365.0 C 46.6 374.6 70.6 380.0 100.0 381.2 C 129.4 380.0 153.4 374.6 165.4 365.0 C 170.2 357.2 171.4 336.2 167.8 311.0 C 153.4 315.2 129.4 317.0 100.0 317.0 Z',
}

// Gradient ids must be unique on the page.
const uid = useId()
const bodyFill = `url(#${uid}-body)`
const roofFill = `url(#${uid}-roof)`
</script>

<template>
  <section v-if="rows.length" class="ov-card ov-status" :aria-label="t('overview.status.title')">
    <header class="ov-card__head">
      <h2 class="ov-card__title">{{ t('overview.status.title') }}</h2>
      <button
        v-if="hasControls"
        type="button"
        class="ov-card__link ov-status__link"
        @click="emit('controls')"
      >
        {{ t('overview.status.controls') }}
      </button>
    </header>
    <div class="ov-status__body">
      <div class="ov-status__car">
        <svg
          class="ov-topview"
          viewBox="-25 0 250 400"
          role="img"
          :aria-label="t('overview.status.drawing')"
        >
          <defs>
            <linearGradient :id="`${uid}-body`" x1="0" x2="1" y1="0" y2="0">
              <stop offset="0" class="ov-topview__shade-edge" />
              <stop offset="0.5" class="ov-topview__shade-mid" />
              <stop offset="1" class="ov-topview__shade-edge" />
            </linearGradient>
            <linearGradient :id="`${uid}-roof`" x1="0" x2="0.5" y1="0" y2="1">
              <stop offset="0" stop-color="#4a505c" />
              <stop offset="0.25" stop-color="#22252c" />
              <stop offset="1" stop-color="#121418" />
            </linearGradient>
          </defs>
          <path v-for="d in CAR.mirrors" :key="d" class="ov-topview__mirror" :d="d" />
          <path class="ov-topview__body" :fill="bodyFill" :d="CAR.body" />
          <!-- Headlights, lit when any lights are on -->
          <path
            v-for="d in CAR.lamps"
            :key="d"
            class="ov-topview__lamp"
            :class="{ 'is-on': lightsOn }"
            :d="d"
          />
          <path class="ov-topview__crease" :d="CAR.crease" />
          <path class="ov-topview__badge" d="M97.5 17.5 h5 l2.5 2.5 v3 l-2.5 2.5 h-5 l-2.5 -2.5 v-3 Z" />
          <!-- Windscreen and roof, black on the two-tone paint -->
          <path class="ov-topview__roof" :fill="roofFill" :d="CAR.roof" />
          <path class="ov-topview__roof-edge" :d="CAR.roofEdge" />
          <!-- The MG4 has no sunroof; one is drawn only when a car reports it open. -->
          <rect
            v-if="s?.sunRoofOpen"
            class="ov-topview__panel is-open"
            x="64"
            y="160"
            width="72"
            height="90"
            rx="8"
          />
          <!-- Rear: black tailgate, the spoiler and its twin fins over the rear glass, the light bar -->
          <path class="ov-topview__rear" :d="CAR.rear" />
          <path class="ov-topview__spoiler" :fill="bodyFill" :d="CAR.spoiler" />
          <path class="ov-topview__taillight" :class="{ 'is-on': lightsOn }" :d="CAR.taillight" />
          <path
            class="ov-topview__panel ov-topview__lid"
            :class="{ 'is-open': s?.bonnetOpen }"
            :d="CAR.bonnet"
          />
          <path
            class="ov-topview__panel ov-topview__lid"
            :class="{ 'is-open': s?.trunkOpen }"
            :d="CAR.boot"
          />
          <rect
            v-for="(shape, corner) in WINDOW_SHAPES"
            :key="`window-${corner}`"
            class="ov-topview__window"
            :class="{ 'is-open': windows[corner] }"
            :x="shape.x"
            :y="shape.y"
            :width="shape.w"
            :height="shape.h"
            rx="1.5"
          />
          <rect
            v-for="(shape, corner) in DOOR_SHAPES"
            :key="`door-${corner}`"
            class="ov-topview__panel ov-topview__door"
            :class="[
              `ov-topview__door--${corner[1] === 'l' ? 'left' : 'right'}`,
              { 'is-open': doors[corner] },
            ]"
            :x="shape.x"
            :y="shape.y"
            :width="shape.w"
            :height="shape.h"
            rx="3"
          >
            <title>{{ cornerLabel(corner) }}</title>
          </rect>
          <!-- Charge port, rear left -->
          <rect
            class="ov-topview__port"
            :class="{ 'is-plugged': cable === 'plugged', 'is-charging': cable === 'charging' }"
            x="23.8"
            y="305"
            width="6"
            height="7.5"
            rx="1.5"
          />
        </svg>
        <span
          v-if="locked !== null"
          class="ov-status__lock"
          :class="locked ? 'is-locked' : 'is-unlocked'"
          :title="locked ? t('overview.status.locked') : t('overview.status.unlocked')"
        >
          <font-awesome-icon :icon="locked ? 'lock' : 'lock-open'" />
        </span>
      </div>
      <dl class="ov-status__list">
        <div v-for="r in rows" :key="r.key">
          <dt>{{ r.label }}</dt>
          <dd :class="`is-${r.tone}`">{{ r.value }}</dd>
        </div>
      </dl>
    </div>
  </section>
</template>
