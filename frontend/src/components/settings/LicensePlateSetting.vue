<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRdwStore } from '@/stores/rdw'
import { useVehicleStore } from '@/stores/vehicle'
import { useErrorMessage } from '@/composables/useErrorMessage'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { formatPlateAs, normalizePlate, plateStyle } from '@/utils/licensePlate'
import LicensePlateInput from './LicensePlateInput.vue'

// The car's plate, kept on the vehicle rather than in the browser's settings, and written the way
// the app's region writes one. The Overview shows it beside the car's name. Where the deployment
// looks Dutch plates up at the RDW, saving looks it up at once, so the dashboard card fills
// straight away.
const { t } = useI18n()
const errorMessage = useErrorMessage()
const rdw = useRdwStore()
const vehicleStore = useVehicleStore()
const style = computed(() => plateStyle(useUiSettingsStore().effectiveRegion))

// As written on the car, dashes or space and all; the API stores it without them.
const saved = computed(() =>
  formatPlateAs(style.value, vehicleStore.activeVehicle?.licensePlate ?? ''),
)
const plate = ref(saved.value)
const busy = ref(false)
const error = ref<string | null>(null)
const done = ref<string | null>(null)

// Follows a plate saved elsewhere (another tab, a device) until the user starts typing here.
watch(saved, (value, previous) => {
  if (plate.value === previous) plate.value = value
})

const changed = computed(() => normalizePlate(plate.value) !== normalizePlate(saved.value))

async function save(value: string) {
  const vin = vehicleStore.activeVin
  if (!vin || busy.value) return
  error.value = null
  done.value = null
  busy.value = true
  try {
    vehicleStore.setLicensePlate(vin, await rdw.setLicensePlate(vin, value))
    plate.value = saved.value
    done.value = !plate.value
      ? t('settings.licensePlate.cleared')
      : rdw.available
        ? t('settings.licensePlate.savedRdw')
        : t('settings.licensePlate.saved')
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="form-stack">
    <p class="settings-toggle__desc">
      {{ t('settings.licensePlate.desc') }}
      <template v-if="rdw.available"> {{ t('settings.licensePlate.descRdw') }}</template>
    </p>

    <form class="license-plate-form" @submit.prevent="save(plate)">
      <LicensePlateInput
        v-model="plate"
        :plate-style="style"
        :electric="vehicleStore.effectiveVehicleType === 'bev'"
        :label="t('settings.licensePlate.label')"
        :placeholder="t(`settings.licensePlate.placeholder.${style}`)"
      />
      <button type="submit" class="btn btn-primary btn-sm" :disabled="busy || !changed">
        {{ t('settings.licensePlate.save') }}
      </button>
      <button
        v-if="saved"
        type="button"
        class="btn btn-outline-secondary btn-sm"
        :disabled="busy"
        @click="save('')"
      >
        {{ t('settings.licensePlate.clear') }}
      </button>
    </form>

    <p v-if="error" class="text-danger text-sm" role="alert">{{ error }}</p>
    <p v-else-if="done" class="text-muted text-sm" role="status">{{ done }}</p>
  </div>
</template>
