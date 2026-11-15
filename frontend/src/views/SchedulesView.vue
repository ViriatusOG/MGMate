<script setup lang="ts">
import '@/assets/climateSchedules.css'
import { onMounted, computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useClimateSchedulesStore } from '@/stores/climateSchedules'
import StatusCard from '@/components/StatusCard.vue'
import ClimateScheduleFormModal from '@/components/ClimateScheduleFormModal.vue'
import type { ClimateSchedule } from '@/services/climateScheduleApi'
import { daysSummary, needsAttention, runLabel, settingsSummary } from '@/utils/climateSchedule'
import { CLIMATE_MODE_ICONS } from '@/utils/climate'
import { formatTimeOfDay } from '@/utils/format'
import { useUnits } from '@/composables/useUnits'
import { useErrorMessage } from '@/composables/useErrorMessage'
import ViewHeader from '@/components/ViewHeader.vue'
import ChargingScheduleCard from '@/components/schedules/ChargingScheduleCard.vue'
import BatteryHeatingScheduleCard from '@/components/schedules/BatteryHeatingScheduleCard.vue'
import { plugsIn } from '@/utils/vehicleType'

const { t } = useI18n()
const vehicleStore = useVehicleStore()
const store = useClimateSchedulesStore()
const units = useUnits()
const errorMessage = useErrorMessage()

const vin = computed(() => vehicleStore.activeVin)

const formOpen = ref(false)
const editing = ref<ClimateSchedule | null>(null)

// The car's latest reported state carries the schedules it keeps itself: reading it never wakes
// the car, only saving a change does.
async function load() {
  await vehicleStore.fetchVehicles()
  if (vin.value)
    await Promise.all([store.fetchSchedules(vin.value), vehicleStore.fetchStatus(vin.value)])
}

onMounted(load)

watch(vin, (v) => {
  if (v) Promise.all([store.fetchSchedules(v), vehicleStore.fetchStatus(v)])
})

const status = computed(() => vehicleStore.currentStatus)
// Only a car that plugs in charges on a schedule or warms its battery for it.
const showCarSchedules = computed(
  () => plugsIn(vehicleStore.effectiveVehicleType) && !!status.value,
)
const canChargeUntilTarget = computed(() => vehicleStore.activeVehicle?.supportsTargetSoc ?? false)

function openAdd() {
  editing.value = null
  formOpen.value = true
}

function openEdit(schedule: ClimateSchedule) {
  editing.value = schedule
  formOpen.value = true
}

function label(schedule: ClimateSchedule): string {
  const days = daysSummary(schedule.days, t)
  return schedule.name ? `${schedule.name} · ${days}` : days
}

// What it sets, then when it runs next: the line a row is told apart by.
function note(schedule: ClimateSchedule): string {
  const when = schedule.nextRunUtc
    ? t('climateSchedules.nextRun', { when: runLabel(schedule.nextRunUtc, new Date(), t) })
    : t('climateSchedules.off')
  return `${settingsSummary(schedule, t, units.value)} · ${when}`
}

function variant(schedule: ClimateSchedule): 'info' | 'warning' | undefined {
  if (!schedule.enabled) return undefined
  return needsAttention(schedule) ? 'warning' : 'info'
}
</script>

<template>
  <div class="view-container">
    <ViewHeader :title="t('schedules.title')" :subtitle="t('schedules.subtitle')" />

    <template v-if="showCarSchedules && status">
      <div class="schedules-car">
        <ChargingScheduleCard
          :vin="vin"
          :status="status"
          :can-charge-until-target="canChargeUntilTarget"
        />
        <BatteryHeatingScheduleCard :vin="vin" :status="status" />
      </div>
      <p class="schedules-hint">
        <font-awesome-icon icon="circle-info" aria-hidden="true" />{{ t('schedules.carHint') }}
      </p>
    </template>

    <section class="schedules-section" aria-labelledby="schedules-climate">
      <div class="schedules-section__head">
        <div>
          <h2 id="schedules-climate" class="schedules-section__title">
            {{ t('schedules.climate.title') }}
          </h2>
          <span class="schedules-section__where">
            <font-awesome-icon icon="clock" aria-hidden="true" />{{ t('schedules.climate.where') }}
          </span>
        </div>
        <button class="btn btn-primary btn-sm" :disabled="!vin" @click="openAdd">
          <font-awesome-icon icon="plus" />{{ t('climateSchedules.addSchedule') }}
        </button>
      </div>

      <div v-if="store.itemsError" class="empty-state text-danger" role="alert">
        <p>{{ errorMessage(store.itemsError) }}</p>
        <button type="button" class="btn btn-sm btn-outline-secondary" @click="load">
          {{ t('common.retry') }}
        </button>
      </div>
      <div v-else-if="!store.loading && store.schedules.length === 0" class="empty-state">
        {{ t('climateSchedules.empty') }}
      </div>
      <div v-else class="status-card-list">
        <StatusCard
          v-for="schedule in store.schedules"
          :key="schedule.id"
          :icon="CLIMATE_MODE_ICONS[schedule.mode]"
          :label="label(schedule)"
          :value="formatTimeOfDay(schedule.startTime)"
          :note="note(schedule)"
          :variant="variant(schedule)"
          clickable
          @click="openEdit(schedule)"
        />
      </div>

      <ClimateScheduleFormModal
        v-if="vin"
        :open="formOpen"
        :vin="vin"
        :schedule="editing"
        @close="formOpen = false"
      />
    </section>
  </div>
</template>

<style scoped>
.schedules-car {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1rem;
  align-items: start;
}

.schedules-hint {
  display: flex;
  gap: 0.5rem;
  align-items: baseline;
  margin: 0.75rem 0 1.75rem;
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.schedules-section__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 0.75rem;
}

.schedules-section__title {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
}

.schedules-section__where {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.75rem;
  color: var(--color-text-muted);
}
</style>
