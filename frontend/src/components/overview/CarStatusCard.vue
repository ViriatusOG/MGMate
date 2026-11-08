<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { regionIsRightHandDrive } from '@/utils/region'

// The car from above, the way Leapmotor Mate draws it: an open door swings out, and an open window,
// boot, bonnet or sunroof turns amber. The list beside it says the same in words. The charge port
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

// Door panels: x, y, height on the 200 × 400 drawing; the hinge is the front edge.
const DOOR_SHAPES: Record<Corner, { x: number; y: number; h: number }> = {
  fl: { x: 40, y: 146, h: 72 },
  rl: { x: 40, y: 222, h: 66 },
  fr: { x: 144, y: 146, h: 72 },
  rr: { x: 144, y: 222, h: 66 },
}
const WINDOW_SHAPES: Record<Corner, { x: number; y: number; h: number }> = {
  fl: { x: 57, y: 160, h: 54 },
  rl: { x: 57, y: 226, h: 46 },
  fr: { x: 137, y: 160, h: 54 },
  rr: { x: 137, y: 226, h: 46 },
}
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
          viewBox="0 0 200 400"
          role="img"
          :aria-label="t('overview.status.drawing')"
        >
          <rect class="ov-topview__wheel" x="30" y="72" width="14" height="46" rx="5" />
          <rect class="ov-topview__wheel" x="156" y="72" width="14" height="46" rx="5" />
          <rect class="ov-topview__wheel" x="30" y="282" width="14" height="46" rx="5" />
          <rect class="ov-topview__wheel" x="156" y="282" width="14" height="46" rx="5" />
          <rect class="ov-topview__body" x="40" y="20" width="120" height="360" rx="48" />
          <ellipse class="ov-topview__body" cx="34" cy="150" rx="9" ry="6" />
          <ellipse class="ov-topview__body" cx="166" cy="150" rx="9" ry="6" />
          <!-- Headlights, lit when any lights are on -->
          <path
            class="ov-topview__lamp"
            :class="{ 'is-on': lightsOn }"
            d="M54 40 Q66 30 80 28 L78 36 Q66 38 56 46 Z"
          />
          <path
            class="ov-topview__lamp"
            :class="{ 'is-on': lightsOn }"
            d="M146 40 Q134 30 120 28 L122 36 Q134 38 144 46 Z"
          />
          <path
            class="ov-topview__panel"
            :class="{ 'is-open': s?.bonnetOpen }"
            d="M50 112 Q100 100 150 112 L152 62 Q100 18 48 62 Z"
          />
          <path class="ov-topview__glass" d="M56 120 Q100 108 144 120 L137 156 Q100 149 63 156 Z" />
          <rect class="ov-topview__glass" x="64" y="160" width="72" height="112" rx="6" />
          <rect
            class="ov-topview__panel"
            :class="{ 'is-open': s?.sunRoofOpen }"
            x="74"
            y="172"
            width="52"
            height="58"
            rx="5"
          />
          <path class="ov-topview__glass" d="M63 276 Q100 283 137 276 L143 304 Q100 314 57 304 Z" />
          <path
            class="ov-topview__panel"
            :class="{ 'is-open': s?.trunkOpen }"
            d="M52 312 Q100 322 148 312 L150 350 Q100 384 50 350 Z"
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
            width="16"
            :height="shape.h"
            rx="3"
          >
            <title>{{ cornerLabel(corner) }}</title>
          </rect>
          <rect
            v-for="(shape, corner) in WINDOW_SHAPES"
            :key="`window-${corner}`"
            class="ov-topview__glass ov-topview__window"
            :class="{ 'is-open': windows[corner] }"
            :x="shape.x"
            :y="shape.y"
            width="6"
            :height="shape.h"
            rx="2"
          />
          <!-- Charge port, rear left -->
          <rect
            class="ov-topview__port"
            :class="{ 'is-plugged': cable === 'plugged', 'is-charging': cable === 'charging' }"
            x="42"
            y="296"
            width="12"
            height="12"
            rx="2"
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
