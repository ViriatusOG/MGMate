<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useUiSettingsStore } from '@/stores/settingsUi'
import type { StatsPreset } from '@/utils/statsPeriod'

// The common periods, always in sight beside the page title; the rest, and dates of one's own,
// stay in the Filters panel.
const CHIPS: readonly StatsPreset[] = ['last24h', 'last7', 'last30', 'last90', 'thisYear']

const { t } = useI18n()
const { statsPeriod } = storeToRefs(useUiSettingsStore())

function choose(preset: StatsPreset) {
  statsPeriod.value = { preset, from: null, to: null }
}
</script>

<template>
  <div class="period-chips" role="group" :aria-label="t('statistics.period.label')">
    <button
      v-for="preset in CHIPS"
      :key="preset"
      type="button"
      class="period-chips__chip"
      :aria-pressed="statsPeriod.preset === preset"
      @click="choose(preset)"
    >
      {{ t(`statistics.period.chip.${preset}`) }}
    </button>
  </div>
</template>

<style scoped>
.period-chips {
  display: inline-flex;
  gap: 0.25rem;
  padding: 0.2rem;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
}

.period-chips__chip {
  padding: 0.25rem 0.6rem;
  border: none;
  border-radius: 6px;
  background: none;
  color: var(--color-text-muted);
  font-size: 0.82rem;
  cursor: pointer;
}

.period-chips__chip[aria-pressed='true'] {
  background: var(--color-surface-2);
  color: var(--color-text);
  font-weight: 600;
}
</style>
