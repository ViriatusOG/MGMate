<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useUnits } from '@/composables/useUnits'
import { formatNumber } from '@/utils/format'
import { chargeSecondsLeft, formatChargeTimeLeft } from '@/utils/chargeTime'
import { runLabel } from '@/utils/climateSchedule'

// How far the car goes, on a phone: the range first, the charge level as the scale under it, and
// one line that says what the charge is doing. The battery card further down keeps the detail.
const { t } = useI18n()
const store = useVehicleStore()
const units = useUnits()

const s = computed(() => store.currentStatus)
const soc = computed(() => s.value?.evSocPercent ?? null)
const limit = computed(() => {
  const target = s.value?.socTargetPercent ?? null
  return target !== null && target > 0 && target < 100 ? target : null
})

const range = computed(() =>
  units.value.measure('distance', s.value?.electricRangeKm ?? null, { decimals: 0 }),
)

// The car's own estimate scaled to another charge level.
function rangeAt(percent: number): string | null {
  const km = s.value?.electricRangeKm ?? null
  if (km === null || soc.value === null || soc.value <= 0) return null
  return units.value.format('distance', (km / soc.value) * percent, { decimals: 0 })
}

const socTone = computed(() => {
  if (soc.value === null) return ''
  if (soc.value < 15) return 'is-low'
  if (soc.value < 30) return 'is-mid'
  return ''
})

const charging = computed(() => s.value?.isCharging === true)

const powerKw = computed(() => {
  const x = s.value
  const hv = x?.hvPower ?? null
  if (hv !== null && hv !== 0) return Math.abs(hv)
  return x?.obcPowerThreePhase ?? x?.obcPowerSinglePhase ?? null
})

// The clock ticks so "at target by" keeps up between the car's reports.
const now = ref(Date.now())
let tick: ReturnType<typeof setInterval> | undefined
onMounted(() => {
  tick = setInterval(() => (now.value = Date.now()), 60_000)
})
onUnmounted(() => clearInterval(tick))

const remainingSec = computed(() => chargeSecondsLeft(s.value?.remainingChargingTime))
const timeLeft = computed(() =>
  charging.value ? formatChargeTimeLeft(remainingSec.value, t) : null,
)

// What the charge is doing, in one line: the power and when it gets to the target while charging,
// otherwise how far the limit and a full battery would go.
const line = computed(() => {
  if (charging.value) {
    const by =
      remainingSec.value !== null
        ? runLabel(
            new Date(now.value + remainingSec.value * 1000).toISOString(),
            new Date(now.value),
            t,
          )
        : null
    return [
      powerKw.value !== null ? `${formatNumber(powerKw.value, 1)} kW` : null,
      limit.value ? t('overview.live.target', { percent: limit.value }) : null,
      by ? `${t('overview.live.fullBy')} ${by}` : null,
    ].filter(Boolean)
  }
  const atLimit = limit.value ? rangeAt(limit.value) : null
  const atFull = rangeAt(100)
  return [
    limit.value && atLimit ? t('overview.atLimit', { percent: limit.value, range: atLimit }) : null,
    atFull ? t('overview.atFull', { range: atFull }) : null,
  ].filter(Boolean)
})
</script>

<template>
  <section v-if="soc !== null || range" class="ov-card ov-prange" :aria-label="t('overview.range')">
    <div class="ov-prange__figures">
      <span v-if="range" class="ov-prange__range">
        {{ range.value }}<small>{{ range.unit }}</small>
      </span>
      <span v-if="range" class="ov-prange__label">{{ t('overview.range').toLowerCase() }}</span>
      <span v-if="soc !== null" class="ov-prange__soc" :class="socTone"
        >{{ Math.round(soc) }}<small>%</small></span
      >
      <span v-if="timeLeft" class="ov-pill ov-pill--success">{{
        t('overview.phone.timeLeft', { time: timeLeft })
      }}</span>
    </div>
    <div
      class="ov-battery__bar"
      role="meter"
      aria-valuemin="0"
      aria-valuemax="100"
      :aria-valuenow="soc ?? undefined"
      :aria-label="t('overview.battery')"
    >
      <span :class="socTone" :style="{ width: `${soc ?? 0}%` }" />
      <i
        v-if="limit"
        :style="{ left: `${limit}%` }"
        :title="t('overview.limit', { percent: limit })"
      />
    </div>
    <p v-if="line.length" class="ov-prange__line">{{ line.join(' · ') }}</p>
  </section>
</template>
