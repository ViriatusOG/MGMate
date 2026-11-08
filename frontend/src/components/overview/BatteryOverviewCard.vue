<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useUnits } from '@/composables/useUnits'
import { useRelativeTime } from '@/composables/useRelativeTime'
import { formatNumber } from '@/utils/format'

// The battery first, the way Leapmotor Mate opens: charge, range, the charge limit on the bar, and
// the few readings people check before they leave.
const { t } = useI18n()
const store = useVehicleStore()
const units = useUnits()
const { relativeTime } = useRelativeTime()

const s = computed(() => store.currentStatus)
const soc = computed(() => s.value?.evSocPercent ?? null)
const limit = computed(() => {
  const target = s.value?.socTargetPercent ?? null
  return target !== null && target > 0 && target < 100 ? target : null
})

function distance(km: number | null | undefined) {
  return km == null ? null : units.value.format('distance', km, { decimals: 0 })
}

const range = computed(() =>
  units.value.measure('distance', s.value?.electricRangeKm ?? null, { decimals: 0 }),
)

// The car's own estimate scaled to another charge level: what it would say at the limit and full.
function rangeAt(percent: number): string | null {
  const km = s.value?.electricRangeKm ?? null
  if (km === null || soc.value === null || soc.value <= 0) return null
  return distance((km / soc.value) * percent)
}

const state = computed(() => {
  const x = s.value
  if (!x) return null
  if ((x.speed ?? 0) > 0) return 'driving'
  if (x.isCharging) return 'charging'
  if (x.chargerConnected) return 'pluggedIn'
  return 'parked'
})

const stateTone = computed(() =>
  state.value === 'charging'
    ? 'success'
    : state.value === 'pluggedIn' || state.value === 'driving'
      ? 'info'
      : null,
)

const socTone = computed(() => {
  if (soc.value === null) return ''
  if (soc.value < 15) return 'is-low'
  if (soc.value < 30) return 'is-mid'
  return ''
})

const twelveVolt = computed(() => {
  const v = s.value?.batteryVoltage ?? null
  return v === null ? null : `${formatNumber(v, 1)} V`
})

const temperature = (c: number | null | undefined) =>
  c == null ? null : units.value.format('temperature', c, { decimals: 0 })

const facts = computed(() => {
  const x = s.value
  if (!x) return []
  const seen = x.lastVehicleStateAt ?? x.recordedAt
  return [
    {
      key: 'odometer',
      label: t('vehicle.odometer'),
      value:
        x.odometerKm == null
          ? null
          : units.value.format('distance', x.odometerKm, { decimals: 0, grouped: true }),
    },
    { key: 'today', label: t('overview.drivenToday'), value: distance(x.mileageOfTheDay) },
    {
      key: 'sinceCharge',
      label: t('overview.sinceCharge'),
      value: distance(x.mileageSinceLastCharge),
    },
    { key: 'cabin', label: t('overview.cabin'), value: temperature(x.interiorTemperature) },
    { key: 'seen', label: t('overview.lastUpdate'), value: seen ? relativeTime(seen) : null },
  ].filter((f) => f.value !== null)
})
</script>

<template>
  <section class="ov-card ov-battery" :aria-label="t('overview.battery')">
    <header class="ov-card__head">
      <h2 class="ov-card__title">{{ t('overview.battery') }}</h2>
      <span v-if="stateTone" class="ov-pill" :class="`ov-pill--${stateTone}`">
        <font-awesome-icon :icon="state === 'driving' ? 'car' : 'plug'" />{{
          t(`overview.states.${state}`)
        }}
      </span>
    </header>
    <div class="ov-battery__figures">
      <span class="ov-battery__soc" :class="socTone">
        <template v-if="soc !== null">{{ Math.round(soc) }}<small>%</small></template>
        <template v-else>-</template>
      </span>
      <span v-if="range" class="ov-battery__range">
        <span class="ov-card__label">{{ t('overview.range') }}</span>
        <span class="ov-battery__range-value"
          >{{ range.value }}<small>{{ range.unit }}</small></span
        >
      </span>
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
    <p class="ov-battery__ranges">
      <span v-if="limit && rangeAt(limit)">{{
        t('overview.atLimit', { percent: limit, range: rangeAt(limit) })
      }}</span>
      <span v-if="rangeAt(100)">{{ t('overview.atFull', { range: rangeAt(100) }) }}</span>
    </p>
    <div class="ov-chips">
      <div v-if="state">
        <small>{{ t('overview.state') }}</small
        ><b>{{ t(`overview.states.${state}`) }}</b>
      </div>
      <div v-if="twelveVolt">
        <small>{{ t('vehicle.battery') }}</small
        ><b>{{ twelveVolt }}</b>
      </div>
      <div v-if="temperature(s?.exteriorTemperature)">
        <small>{{ t('overview.outside') }}</small
        ><b>{{ temperature(s?.exteriorTemperature) }}</b>
      </div>
    </div>
    <dl class="ov-facts">
      <div v-for="f in facts" :key="f.key">
        <dt>{{ f.label }}</dt>
        <dd>{{ f.value }}</dd>
      </div>
    </dl>
  </section>
</template>
