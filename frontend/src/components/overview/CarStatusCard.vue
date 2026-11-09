<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import CarStatusList from './CarStatusList.vue'
import Mg4TopView from './Mg4TopView.vue'
import { useCarStatus } from './useCarStatus'

// The car's status on a card of its own: the MG4 drawing with the list beside it. The Overview
// card shows the same, with the tyre pressures, so the dashboard only shows this one when the
// Overview card is hidden.
defineProps<{
  /** Whether the overview shows its controls row, which the link scrolls to. */
  hasControls?: boolean
}>()
const emit = defineEmits<{ controls: [] }>()

const { t } = useI18n()
const { rows, locked } = useCarStatus()
</script>

<template>
  <section v-if="rows.length" class="ov-card ov-status" :aria-label="t('overview.status.title')">
    <header class="ov-card__head">
      <h2 class="ov-card__title">{{ t('overview.status.title') }}</h2>
      <button
        v-if="hasControls"
        type="button"
        class="ov-card__link ov-status__link"
        @click="emit('controls')"
      >
        {{ t('overview.status.controls') }}
      </button>
    </header>
    <div class="ov-status__body">
      <div class="ov-status__car">
        <Mg4TopView />
        <span
          v-if="locked !== null"
          class="ov-status__lock"
          :class="locked ? 'is-locked' : 'is-unlocked'"
          :title="locked ? t('overview.status.locked') : t('overview.status.unlocked')"
        >
          <font-awesome-icon :icon="locked ? 'lock' : 'lock-open'" />
        </span>
      </div>
      <CarStatusList :rows="rows" />
    </div>
  </section>
</template>
