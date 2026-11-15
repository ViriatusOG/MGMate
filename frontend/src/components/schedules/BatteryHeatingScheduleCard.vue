<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import CarScheduleCard from './CarScheduleCard.vue'
import BatteryHeatingScheduleForm from '@/components/BatteryHeatingScheduleForm.vue'
import type { TelemetrySnapshot } from '@/services/vehicleApi'
import { timeOfDay } from '@/utils/dates'
import { formatTimeOfDay } from '@/utils/format'
import { nextDailyStart } from '@/utils/carSchedule'

// The battery heating time the car keeps itself, every day.
const props = defineProps<{
  vin: string | null
  status: TelemetrySnapshot
}>()

const { t } = useI18n()

// The gateway reports "on" and "off", though a car has been seen saying "timed": anything but off
// counts as on, as on the Overview's battery heating card.
const on = computed(() => {
  const mode = props.status.batteryHeatingScheduleMode
  return !!mode && mode !== 'off'
})
const start = computed(() => timeOfDay(props.status.batteryHeatingScheduleStartTime))
const time = computed(() => (on.value && start.value ? formatTimeOfDay(start.value) : null))

const rows = computed(() => {
  const next = on.value ? nextDailyStart(start.value, new Date(), t) : null
  return next ? [{ label: t('schedules.nextStart'), value: next }] : []
})
</script>

<template>
  <CarScheduleCard
    icon="temperature-arrow-up"
    :title="t('schedules.heating.title')"
    :time="time"
    :line="on ? t('schedules.heating.onLine') : t('schedules.heating.offLine')"
    :on="on"
    :rows="rows"
  >
    <BatteryHeatingScheduleForm
      :vin="vin"
      :mode="status.batteryHeatingScheduleMode"
      :start-time="status.batteryHeatingScheduleStartTime"
    />
  </CarScheduleCard>
</template>
