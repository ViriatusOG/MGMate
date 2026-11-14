<script setup lang="ts">
import '@/assets/overview.css'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useLiveCharge } from '@/composables/useLiveCharge'
import { useVehicleCommand } from '@/composables/useVehicleCommand'
import { formatNumber, formatTime } from '@/utils/format'
import { formatChargeTimeLeft } from '@/utils/chargeTime'
import { runLabel } from '@/utils/climateSchedule'
import CommandButton from '@/components/CommandButton.vue'
import CommandFeedback from '@/components/CommandFeedback.vue'
import OverviewChart from '@/components/overview/OverviewChart.vue'
import type { ChartSeries } from '@/components/overview/chart'

// The charge under way, at the top of the Charges page on a phone: how full, how fast and until
// when, the one thing to do about it (stop it), and its level drawn from where it began to where
// the car says it will end. Nothing is shown while the car is not charging.
const { t } = useI18n()
const store = useVehicleStore()
const { timeline, now, charging, chargeStart, powerKw, chargeKind, remainingSec, addedKwh } =
  useLiveCharge()
const { sending, lastResult, isPending, send } = useVehicleCommand()

const s = computed(() => store.currentStatus)
const soc = computed(() => s.value?.evSocPercent ?? null)
const limit = computed(() => {
  const target = s.value?.socTargetPercent ?? null
  return target !== null && target > 0 && target < 100 ? target : null
})

const timeLeft = computed(() => formatChargeTimeLeft(remainingSec.value, t))
const endsAt = computed(() =>
  remainingSec.value !== null ? now.value + remainingSec.value * 1000 : null,
)

const electrics = computed(() => {
  const x = s.value
  return [
    x?.hvVoltage != null ? `${formatNumber(x.hvVoltage, 0)} V` : null,
    x?.hvCurrent != null ? `${formatNumber(Math.abs(x.hvCurrent), 0)} A` : null,
  ]
    .filter(Boolean)
    .join(' · ')
})

const startedLine = computed(() => {
  const { at, soc: from } = chargeStart.value
  if (at === null) return null
  const time = formatTime(new Date(at), { hour: '2-digit', minute: '2-digit' })
  return from !== null
    ? t('charges.live.startedAtFrom', { time, percent: Math.round(from) })
    : t('charges.live.startedAt', { time })
})

const targetBy = computed(() =>
  endsAt.value !== null
    ? runLabel(new Date(endsAt.value).toISOString(), new Date(now.value), t)
    : null,
)

// The level since this charge began, and a dashed line on to the target at the car's own time.
const chart = computed(() => {
  const from = chargeStart.value.at
  if (from === null || soc.value === null) return null
  const level: [number, number][] = (timeline.value?.soc ?? [])
    .map((p): [number, number] => [Date.parse(p.at), p.percent])
    .filter(([at]) => at >= from)
  if (chargeStart.value.soc !== null && (level.length === 0 || level[0]![0] > from))
    level.unshift([from, chargeStart.value.soc])
  level.push([now.value, soc.value])
  if (level.length < 2) return null
  const series: ChartSeries[] = [
    {
      points: level,
      tone: 'success',
      name: t('overview.chart.soc'),
      unit: '%',
      min: 0,
      max: 100,
      fill: true,
    },
  ]
  const goal = limit.value ?? 100
  if (endsAt.value !== null && goal > soc.value)
    series.push({
      points: [
        [now.value, soc.value],
        [endsAt.value, goal],
      ],
      tone: 'success',
      name: t('charges.live.forecast'),
      unit: '%',
      axis: 'left',
      min: 0,
      max: 100,
      dashed: true,
    })
  return { series, from, to: endsAt.value ?? now.value }
})

// Taken as done once the car reports it has stopped, not on the gateway's word alone.
function stop() {
  const vin = store.activeVin
  if (vin) send(vin, 'charging', 'false', { isConfirmed: (x) => x.isCharging === false })
}
</script>

<template>
  <section v-if="charging" class="ch-live" :aria-label="t('overview.live.title')">
    <div class="ov-card ov-battery--charging ch-live__card">
      <header class="ch-live__head">
        <h2 class="ov-card__title ov-live__title">
          <span class="ov-live__pulse" aria-hidden="true" />{{ t('overview.live.title')
          }}<template v-if="chargeKind"> · {{ t(`overview.live.${chargeKind}`) }}</template>
        </h2>
        <span v-if="limit" class="ov-pill ov-pill--plain">{{
          t('overview.live.target', { percent: limit })
        }}</span>
        <span v-if="timeLeft" class="ov-pill ov-pill--success ch-live__left">{{
          t('overview.phone.timeLeft', { time: timeLeft })
        }}</span>
      </header>

      <div class="ch-live__main">
        <div
          v-if="soc !== null"
          class="ch-live__ring"
          :style="{ '--p': Math.round(soc) }"
          role="img"
          :aria-label="`${Math.round(soc)}%`"
        >
          <span>
            <b>{{ Math.round(soc) }}%</b>
            <small v-if="limit">{{ t('charges.live.ofTarget', { percent: limit }) }}</small>
          </span>
        </div>
        <div class="ch-live__power">
          <span class="ov-live__power" :title="t('overview.live.power')">
            <template v-if="powerKw !== null"
              >{{ formatNumber(powerKw, 1) }}<small> kW</small></template
            >
            <template v-else>-</template>
          </span>
          <span v-if="electrics" class="ch-live__sub">{{ electrics }}</span>
        </div>
      </div>

      <div
        v-if="soc !== null"
        class="ov-battery__bar"
        role="meter"
        aria-valuemin="0"
        aria-valuemax="100"
        :aria-valuenow="Math.round(soc)"
        :aria-label="t('overview.battery')"
      >
        <span :style="{ width: `${soc}%` }" />
        <i v-if="limit" :style="{ left: `${limit}%` }" />
      </div>

      <p v-if="addedKwh !== null" class="ch-live__line">
        {{ t('overview.live.added') }}
        <strong>{{ formatNumber(addedKwh, 1) }} {{ t('common.kwh') }}</strong>
      </p>
      <p v-if="startedLine || targetBy" class="ch-live__line">
        <template v-if="startedLine">{{ startedLine }}</template>
        <template v-if="startedLine && targetBy"> · </template>
        <template v-if="targetBy"
          >{{ t('charges.live.targetBy') }} <strong>{{ targetBy }}</strong></template
        >
      </p>
      <p class="ov-live__note">{{ t('overview.live.note') }}</p>
    </div>

    <CommandButton
      class="btn-outline-secondary ch-live__stop"
      :pending="isPending('charging')"
      :sending="sending === 'charging'"
      :disabled="!store.activeVin"
      icon="power-off"
      :label="t('control.stopChargingLong')"
      :confirm-label="t('charges.live.stopConfirm')"
      @click="stop"
    />
    <CommandFeedback
      command="charging"
      :pending="isPending('charging')"
      :last-result="lastResult"
    />

    <div v-if="chart" class="ov-card ch-live__chart">
      <h2 class="ov-card__title">{{ t('charges.live.thisCharge') }}</h2>
      <OverviewChart
        :series="chart.series"
        :from="chart.from"
        :to="chart.to"
        :label="t('charges.live.thisCharge')"
      />
    </div>
  </section>
</template>
