<script setup lang="ts">
import { computed, ref, shallowRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useUnits } from '@/composables/useUnits'
import {
  useTyreDisplay,
  type TyrePosition,
  type TyrePressures,
} from '@/components/carDiagram/useTyreDisplay'
import Mg4TopView from './Mg4TopView.vue'
import { useCarStatus, type Corner, type Tone } from './useCarStatus'

// The car on a phone: the MG4 from above on the left, small, and what it reports on the right.
// Whatever is open, and the cable when it is in, gets a number on the drawing and the same
// number on its row, so the list reads as an index of the drawing. The tyre pressures sit in a
// strip underneath, where there is room for them.
const props = defineProps<{ pressures: TyrePressures }>()

const { t } = useI18n()
const units = useUnits()
const tyres = useTyreDisplay()
const { status, doors, windows, cable, locked, compactRows, cornerLabel } = useCarStatus()

interface Row {
  key: string
  label: string
  value: string
  tone: Tone
  /** The number shared with the marker on the drawing, for a part that has one. */
  n?: number
  /** Where the marker sits, in percent of the drawing's box. */
  at?: { x: number; y: number }
}

const CORNERS: Corner[] = ['fl', 'fr', 'rl', 'rr']
// The drawing's view box is 250 × 400 with the car's body between x 25 and 225 of it.
const DOOR_AT: Record<Corner, { x: number; y: number }> = {
  fl: { x: 12, y: 50 },
  fr: { x: 88, y: 50 },
  rl: { x: 12, y: 68 },
  rr: { x: 88, y: 68 },
}
// A window's marker sits above and inboard of its door's, so both can show on one corner.
const WINDOW_AT: Record<Corner, { x: number; y: number }> = {
  fl: { x: 30, y: 36 },
  fr: { x: 70, y: 36 },
  rl: { x: 30, y: 60 },
  rr: { x: 70, y: 60 },
}
const PANEL_AT = {
  boot: { x: 50, y: 91 },
  bonnet: { x: 50, y: 13 },
  sunroof: { x: 50, y: 52 },
  cable: { x: 15, y: 77 },
} as const

const rows = computed((): Row[] => {
  const out: Row[] = []
  let n = 0
  const all = compactRows.value
  const closed = (key: string) => all.find((r) => r.key === key)?.tone === 'ok'
  // Boot and bonnet share a row while both are shut, to keep the list as short as the car.
  const lidsShut = closed('boot') && closed('bonnet')
  for (const row of all) {
    // The lock has its place in the card's header, and lights that are off say nothing.
    if (row.key === 'locks' || (row.key === 'lights' && row.tone === 'muted')) continue
    if (lidsShut && row.key === 'bonnet') continue
    if (lidsShut && row.key === 'boot') {
      out.push({ ...row, key: 'lids', label: t('overview.phone.lids') })
      continue
    }
    if (row.key === 'doors' || row.key === 'windows') {
      const map = row.key === 'doors' ? doors.value : windows.value
      const at = row.key === 'doors' ? DOOR_AT : WINDOW_AT
      const open = CORNERS.filter((c) => map[c] === true)
      if (open.length === 0) {
        out.push(row)
        continue
      }
      // One row for each open door or window, named by its corner.
      for (const c of open) {
        out.push({
          key: `${row.key}-${c}`,
          label: t(row.key === 'doors' ? 'overview.phone.door' : 'overview.phone.window', {
            corner: cornerLabel(c),
          }),
          value: t('overview.status.open'),
          tone: 'warn',
          n: ++n,
          at: at[c],
        })
      }
    } else if (row.key in PANEL_AT && (row.tone === 'warn' || row.key === 'cable')) {
      const plugged = row.key !== 'cable' || (cable.value !== 'none' && cable.value !== null)
      out.push(
        plugged ? { ...row, n: ++n, at: PANEL_AT[row.key as keyof typeof PANEL_AT] } : { ...row },
      )
    } else {
      out.push(row)
    }
  }
  return out
})

const markers = computed(() => rows.value.filter((r) => r.n !== undefined && r.at))

const speed = computed(() => {
  const kmh = status.value?.speed ?? 0
  return kmh > 0 ? units.value.format('speed', kmh, { decimals: 0 }) : null
})

const state = computed(() => {
  if (speed.value) return 'driving'
  if (cable.value === 'charging') return 'charging'
  if (cable.value === 'plugged') return 'pluggedIn'
  return status.value ? 'parked' : null
})

const stateTone = computed(() =>
  state.value === 'charging' ? 'success' : state.value === 'parked' ? 'plain' : 'info',
)

const TYRES: { position: TyrePosition; short: string }[] = [
  { position: 'frontLeft', short: 'FL' },
  { position: 'frontRight', short: 'FR' },
  { position: 'rearLeft', short: 'RL' },
  { position: 'rearRight', short: 'RR' },
]
const hasTyres = computed(() => TYRES.some((x) => props.pressures[x.position] !== null))

// The 30 days of tyre pressures open from the strip, as they do from the wide card's labels.
const TyreHistoryModal = shallowRef<
  typeof import('@/components/TyreHistoryModal.vue').default | null
>(null)
const tyreHistoryOpen = ref(false)
async function openTyreHistory() {
  TyreHistoryModal.value ??= (await import('@/components/TyreHistoryModal.vue')).default
  tyreHistoryOpen.value = true
}
</script>

<template>
  <section v-if="rows.length" class="ov-card ov-pcar" :aria-label="t('overview.status.title')">
    <header class="ov-pcar__head">
      <span v-if="state" class="ov-pill" :class="`ov-pill--${stateTone}`">
        {{ t(`overview.states.${state}`) }}<template v-if="speed"> · {{ speed }}</template>
      </span>
      <span v-if="locked !== null" class="ov-pcar__lock" :class="locked ? 'is-ok' : 'is-warn'">
        <font-awesome-icon :icon="locked ? 'lock' : 'lock-open'" aria-hidden="true" />
        {{ locked ? t('overview.status.locked') : t('overview.status.unlocked') }}
      </span>
    </header>

    <div class="ov-pcar__body">
      <div class="ov-pcar__car">
        <Mg4TopView />
        <span
          v-for="m in markers"
          :key="m.key"
          class="ov-pcar__mark"
          :class="`is-${m.tone}`"
          :style="{ left: `${m.at!.x}%`, top: `${m.at!.y}%` }"
          aria-hidden="true"
          >{{ m.n }}</span
        >
      </div>

      <dl class="ov-pcar__list">
        <div v-for="r in rows" :key="r.key">
          <dt>
            <span v-if="r.n !== undefined" class="ov-pcar__n" :class="`is-${r.tone}`">{{
              r.n
            }}</span
            >{{ r.label }}
          </dt>
          <dd :class="`is-${r.tone}`">{{ r.value }}</dd>
        </div>
      </dl>
    </div>

    <button
      v-if="hasTyres"
      type="button"
      class="ov-pcar__tyres"
      :aria-label="t('overview.phone.tyresOpen')"
      @click="openTyreHistory"
    >
      <span class="ov-pcar__tyres-label">{{ t('overview.phone.tyres') }}</span>
      <span
        v-for="x in TYRES"
        :key="x.position"
        class="ov-pcar__tyre"
        :class="`is-${tyres.variant(pressures[x.position])}`"
        :title="t(`vehicle.diagram.tyrePosition.${x.position}`)"
      >
        <small>{{ x.short }}</small
        >{{ tyres.reading(pressures[x.position]) }}
      </span>
    </button>

    <component
      :is="TyreHistoryModal"
      v-if="TyreHistoryModal"
      :open="tyreHistoryOpen"
      @close="tyreHistoryOpen = false"
    />
  </section>
</template>
