<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useUnits } from '@/composables/useUnits'
import { useRelativeTime } from '@/composables/useRelativeTime'
import { useLiveCharge } from '@/composables/useLiveCharge'
import { formatNumber } from '@/utils/format'
import { formatDurationMs } from '@/utils/tripRows'
import { formatChargeTimeLeft } from '@/utils/chargeTime'
import { runLabel } from '@/utils/climateSchedule'
import OverviewChart from './OverviewChart.vue'
import type { ChartSeries } from './chart'

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
    { key: 'cabin', label: t('overview.cabin'), value: temperature(x.interiorTemperature) },
    { key: 'seen', label: t('overview.lastUpdate'), value: seen ? relativeTime(seen) : null },
  ].filter((f) => f.value !== null)
})

// ── The last 24 hours, for the level line and the start of a charge under way ──

const DAY_MS = 86_400_000
const { timeline, now, charging, chargeStart, powerKw, chargeKind, remainingSec, addedKwh } =
  useLiveCharge()

const socSeries = computed((): ChartSeries[] => {
  const points: [number, number][] = (timeline.value?.soc ?? []).map((p) => [
    Date.parse(p.at),
    p.percent,
  ])
  if (soc.value !== null) points.push([now.value, soc.value])
  if (points.length < 2) return []
  return [
    {
      points,
      tone: 'success',
      name: t('overview.chart.soc'),
      unit: '%',
      min: 0,
      max: 100,
      fill: true,
    },
  ]
})

const socDayFrom = computed(() => {
  const first = timeline.value?.soc?.[0]
  return first ? Math.round(first.percent) : null
})

// ── Charging, live ──

const live = computed(() => {
  const x = s.value
  if (!x || !charging.value) return null
  const start = chargeStart.value
  const rangeKm = x.electricRangeKm ?? null
  const rangeGain =
    rangeKm !== null && start.soc !== null && soc.value
      ? rangeKm - (rangeKm / soc.value) * start.soc
      : null
  const rows = [
    {
      key: 'fullBy',
      label: t('overview.live.fullBy'),
      value:
        remainingSec.value !== null
          ? runLabel(
              new Date(now.value + remainingSec.value * 1000).toISOString(),
              new Date(now.value),
              t,
            )
          : null,
    },
    {
      key: 'added',
      label: t('overview.live.added'),
      value:
        addedKwh.value !== null ? `+${formatNumber(addedKwh.value, 1)} ${t('common.kwh')}` : null,
    },
    {
      key: 'range',
      label: t('overview.live.rangeNow'),
      value:
        rangeKm === null
          ? null
          : [
              distance(rangeKm),
              rangeGain !== null && rangeGain > 0 ? `(+${distance(rangeGain)})` : null,
            ]
              .filter(Boolean)
              .join(' '),
    },
    {
      key: 'for',
      label: t('overview.live.chargingFor'),
      value:
        start.at !== null && now.value > start.at
          ? formatDurationMs(now.value - start.at, t)
          : null,
    },
  ].filter((r) => r.value)
  return {
    power: powerKw.value !== null ? formatNumber(powerKw.value, 1) : null,
    voltage: x.hvVoltage != null ? `${formatNumber(x.hvVoltage, 0)} V` : null,
    current: x.hvCurrent != null ? `${formatNumber(Math.abs(x.hvCurrent), 0)} A` : null,
    timeLeft: formatChargeTimeLeft(remainingSec.value, t),
    startSoc: start.soc,
    rows,
  }
})
</script>

<template>
  <section
    class="ov-card ov-battery"
    :class="{ 'ov-battery--charging': live }"
    :aria-label="live ? t('overview.live.title') : t('overview.battery')"
  >
    <template v-if="live">
      <header class="ov-card__head">
        <h2 class="ov-card__title ov-live__title">
          <span class="ov-live__pulse" aria-hidden="true" />{{ t('overview.live.title')
          }}<template v-if="chargeKind"> · {{ t(`overview.live.${chargeKind}`) }}</template>
        </h2>
        <span v-if="limit" class="ov-card__label">{{
          t('overview.live.target', { percent: limit })
        }}</span>
      </header>
      <div class="ov-battery__figures">
        <span class="ov-live__power" :title="t('overview.live.power')">
          <template v-if="live.power">{{ live.power }}<small> kW</small></template>
          <template v-else>-</template>
        </span>
        <span class="ov-battery__soc ov-battery__soc--small" :class="socTone">
          <template v-if="soc !== null">{{ Math.round(soc) }}<small>%</small></template>
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
        <span
          v-if="live.startSoc !== null"
          class="ov-battery__bar-before"
          :style="{ width: `${live.startSoc}%` }"
        />
        <span
          class="ov-battery__bar-now"
          :style="
            live.startSoc !== null
              ? { left: `${live.startSoc}%`, width: `${Math.max(0, (soc ?? 0) - live.startSoc)}%` }
              : { width: `${soc ?? 0}%` }
          "
        />
        <i v-if="limit" :style="{ left: `${limit}%` }" />
      </div>
      <p class="ov-battery__ranges">
        <span v-if="live.startSoc !== null">{{
          t('overview.live.started', { percent: Math.round(live.startSoc) })
        }}</span>
        <span v-if="limit">{{ t('overview.live.target', { percent: limit }) }}</span>
      </p>
      <div class="ov-chips">
        <div v-if="live.voltage">
          <small>{{ t('overview.live.voltage') }}</small
          ><b>{{ live.voltage }}</b>
        </div>
        <div v-if="live.current">
          <small>{{ t('overview.live.current') }}</small
          ><b>{{ live.current }}</b>
        </div>
        <div v-if="live.timeLeft">
          <small>{{ t('overview.live.timeLeft') }}</small
          ><b>{{ live.timeLeft }}</b>
        </div>
      </div>
      <dl class="ov-facts">
        <div v-for="r in live.rows" :key="r.key">
          <dt>{{ r.label }}</dt>
          <dd>{{ r.value }}</dd>
        </div>
      </dl>
    </template>

    <template v-else>
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
    </template>

    <figure v-if="socSeries.length" class="ov-spark-wrap">
      <OverviewChart
        :series="socSeries"
        :from="now - DAY_MS"
        :to="now"
        ends-now
        :label="t('overview.socDay')"
      />
      <figcaption class="ov-spark-cap">
        <span>{{ t('overview.chart.last24h') }}</span>
        <span v-if="socDayFrom !== null && soc !== null"
          >{{ socDayFrom }}% → {{ Math.round(soc) }}%</span
        >
      </figcaption>
    </figure>
    <p v-if="live" class="ov-live__note">{{ t('overview.live.note') }}</p>
  </section>
</template>
