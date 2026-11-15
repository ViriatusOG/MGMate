<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import CarScheduleCard from './CarScheduleCard.vue'
import ChargingScheduleForm from '@/components/ChargingScheduleForm.vue'
import type { TelemetrySnapshot } from '@/services/vehicleApi'
import { timeOfDay } from '@/utils/dates'
import { formatTimeOfDay } from '@/utils/format'
import { chargeCurrentText, nextDailyStart } from '@/utils/carSchedule'

// The charging schedule the car keeps itself: one window, every day, used whenever it is plugged in.
const props = defineProps<{
  vin: string | null
  status: TelemetrySnapshot
  /** Whether the car takes a charge target, and so can charge until it. */
  canChargeUntilTarget: boolean
}>()

const { t } = useI18n()

const mode = computed(() => props.status.chargingScheduleMode)
const on = computed(() => !!mode.value && mode.value !== 'DISABLED')
const untilTarget = computed(() => mode.value === 'UNTIL_CONFIGURED_SOC')
const start = computed(() => timeOfDay(props.status.chargingScheduleStartTime))
const end = computed(() => timeOfDay(props.status.chargingScheduleEndTime))
const target = computed(() => props.status.socTargetPercent ?? null)

const time = computed(() => {
  if (!on.value || !start.value) return null
  if (untilTarget.value || !end.value) return formatTimeOfDay(start.value)
  return `${formatTimeOfDay(start.value)} – ${formatTimeOfDay(end.value)}`
})

const line = computed(() => {
  if (!on.value) return t('schedules.charging.offLine')
  if (untilTarget.value && target.value !== null)
    return t('schedules.charging.untilTargetLine', { percent: target.value })
  return t('schedules.charging.untilTimeLine')
})

const rows = computed(() => {
  const rows: { label: string; value: string }[] = []
  const next = on.value ? nextDailyStart(start.value, new Date(), t) : null
  if (next) rows.push({ label: t('schedules.nextStart'), value: next })
  if (target.value !== null)
    rows.push({ label: t('schedules.charging.target'), value: `${target.value}%` })
  const current = chargeCurrentText(props.status.chargeCurrentLimit, t)
  if (current) rows.push({ label: t('schedules.charging.current'), value: current })
  return rows
})
</script>

<template>
  <CarScheduleCard
    icon="plug-circle-bolt"
    :title="t('schedules.charging.title')"
    :time="time"
    :line="line"
    :on="on"
    :rows="rows"
  >
    <ChargingScheduleForm
      :vin="vin"
      :mode="mode"
      :start-time="status.chargingScheduleStartTime"
      :end-time="status.chargingScheduleEndTime"
      :can-charge-until-target="canChargeUntilTarget"
      :target="target"
    />
  </CarScheduleCard>
</template>
