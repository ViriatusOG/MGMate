<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { CAR_NAME_MAX_LENGTH, useUiSettingsStore } from '@/stores/settingsUi'
import { useVehicleStore } from '@/stores/vehicle'

// What the owner calls the car. MG names a car its own way, which need not be the name on the
// brochure, so the owner may write their own. It is a synced setting, and the server serves it
// as the car's name, so every page here and the Android app show it.
const { t } = useI18n()
const settings = useUiSettingsStore()
const vehicleStore = useVehicleStore()

// The name MG reports, whatever the owner's own name is.
const reported = computed(() => {
  const first = vehicleStore.vehicles[0]
  return (first?.reportedModel === undefined ? first?.model : first.reportedModel) ?? null
})

const saved = computed(() => settings.carName ?? '')
const name = ref(saved.value)
const done = ref<string | null>(null)

// Follows a name saved elsewhere (another tab, a device) until the user starts typing here.
watch(saved, (value, previous) => {
  if (name.value === previous) name.value = value
})

const changed = computed(() => name.value.trim() !== saved.value)

function save(value: string) {
  const trimmed = value.trim().slice(0, CAR_NAME_MAX_LENGTH)
  settings.carName = trimmed || null
  name.value = trimmed
  done.value = trimmed ? t('settings.carName.saved') : t('settings.carName.cleared')
}
</script>

<template>
  <div class="form-stack">
    <p class="settings-toggle__desc">
      {{ t('settings.carName.desc') }}
      <template v-if="reported">
        {{ t('settings.carName.descReported', { name: reported }) }}
      </template>
    </p>

    <form class="license-plate-form" @submit.prevent="save(name)">
      <input
        v-model="name"
        type="text"
        class="form-control form-control-sm car-name-input"
        autocomplete="off"
        :maxlength="CAR_NAME_MAX_LENGTH"
        :aria-label="t('settings.carName.label')"
        :placeholder="reported ?? ''"
      />
      <button type="submit" class="btn btn-primary btn-sm" :disabled="!changed">
        {{ t('settings.carName.save') }}
      </button>
      <button
        v-if="saved"
        type="button"
        class="btn btn-outline-secondary btn-sm"
        @click="save('')"
      >
        {{ t('settings.carName.clear') }}
      </button>
    </form>

    <p v-if="done" class="text-muted text-sm" role="status">{{ done }}</p>
  </div>
</template>

<style scoped>
.car-name-input {
  flex: 1 1 12rem;
  min-width: 0;
  max-width: 22rem;
}
</style>
