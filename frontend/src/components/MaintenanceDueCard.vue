<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MaintenanceItem } from '@/services/maintenanceApi'
import { dueProgress, dueStatusVariant, formatIntervalSummary } from '@/utils/maintenance'
import { ODOMETER_FORMAT } from '@/utils/units'
import { formatDate } from '@/utils/format'
import { useUnits } from '@/composables/useUnits'

const props = defineProps<{ item: MaintenanceItem }>()
defineEmits<{ (e: 'open'): void }>()

const { t } = useI18n()
const units = useUnits()

const variant = computed(() => dueStatusVariant(props.item.dueStatus))
const progress = computed(() => dueProgress(props.item))

function distance(km: number): string {
  return units.value.format('distance', km, ODOMETER_FORMAT) ?? '-'
}

const bars = computed(() => {
  const out: { key: string; used: number; text: string; over: boolean }[] = []
  const { distance: d, time } = progress.value
  if (d)
    out.push({
      key: 'distance',
      used: d.used,
      over: d.remaining < 0,
      text:
        d.remaining < 0
          ? t('maintenance.over', { amount: distance(-d.remaining) })
          : t('maintenance.left', { amount: distance(d.remaining) }),
    })
  if (time)
    out.push({
      key: 'time',
      used: time.used,
      over: time.remaining < 0,
      text:
        time.remaining < 0
          ? t('maintenance.overDays', Math.round(-time.remaining))
          : t('maintenance.leftDays', Math.round(time.remaining)),
    })
  return out
})

const dueLine = computed(() => {
  const parts: string[] = []
  if (props.item.nextDueDate)
    parts.push(
      formatDate(props.item.nextDueDate, { day: 'numeric', month: 'short', year: 'numeric' }),
    )
  if (props.item.nextDueOdometerKm != null)
    parts.push(t('maintenance.atOdometer', { distance: distance(props.item.nextDueOdometerKm) }))
  return parts.length ? t('maintenance.dueOn', { when: parts.join(' · ') }) : null
})

const lastLine = computed(() => {
  const { lastServiceDate, lastServiceOdometerKm } = props.item
  if (!lastServiceDate && lastServiceOdometerKm == null) return t('maintenance.neverDone')
  const parts: string[] = []
  if (lastServiceDate)
    parts.push(formatDate(lastServiceDate, { day: 'numeric', month: 'short', year: 'numeric' }))
  if (lastServiceOdometerKm != null) parts.push(distance(lastServiceOdometerKm))
  return t('maintenance.lastDone', { when: parts.join(' · ') })
})
</script>

<template>
  <button type="button" class="due-card" :class="`due-card--${variant}`" @click="$emit('open')">
    <span class="due-card__head">
      <span class="due-card__name">{{ item.name }}</span>
      <span class="due-card__badge">{{ t(`maintenance.status.${item.dueStatus}`) }}</span>
    </span>
    <span class="due-card__interval">{{ formatIntervalSummary(item, t, units) }}</span>
    <span v-for="bar in bars" :key="bar.key" class="due-card__bar-row">
      <font-awesome-icon
        :icon="bar.key === 'time' ? 'calendar-days' : 'road'"
        class="due-card__bar-icon"
      />
      <span class="due-card__bar" role="presentation">
        <span class="due-card__bar-fill" :style="{ width: `${Math.max(bar.used * 100, 2)}%` }" />
      </span>
      <span class="due-card__bar-text" :class="{ 'is-over': bar.over }">{{ bar.text }}</span>
    </span>
    <span v-if="dueLine" class="due-card__meta">{{ dueLine }}</span>
    <span class="due-card__meta">{{ lastLine }}</span>
  </button>
</template>

<style scoped>
.due-card {
  --tone: var(--color-success, #22c55e);

  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  width: 100%;
  padding: 0.875rem 1rem;
  font: inherit;
  color: inherit;
  text-align: left;
  cursor: pointer;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-left: 4px solid var(--tone);
  border-radius: var(--radius);
}

.due-card:hover {
  background: var(--color-surface-2);
}

.due-card--warning {
  --tone: var(--color-warning, #f59e0b);
}

.due-card--danger {
  --tone: var(--color-danger, #ef4444);
}

.due-card--info {
  --tone: var(--color-text-muted);
}

.due-card__head {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  justify-content: space-between;
}

.due-card__name {
  min-width: 0;
  overflow: hidden;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.due-card__badge {
  flex-shrink: 0;
  padding: 0.125rem 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--tone);
  background: color-mix(in srgb, var(--tone) 15%, transparent);
  border-radius: 999px;
}

.due-card__interval,
.due-card__meta {
  font-size: 0.8rem;
  color: var(--color-text-muted);
}

.due-card__bar-row {
  display: grid;
  grid-template-columns: 1rem minmax(0, 1fr) auto;
  gap: 0.5rem;
  align-items: center;
}

.due-card__bar-icon {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.due-card__bar {
  height: 0.5rem;
  overflow: hidden;
  background: var(--color-surface-2);
  border-radius: 999px;
}

.due-card__bar-fill {
  display: block;
  height: 100%;
  background: var(--tone);
  border-radius: inherit;
}

.due-card__bar-text {
  font-size: 0.8rem;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.due-card__bar-text.is-over {
  font-weight: 600;
  color: var(--color-danger, #ef4444);
}
</style>
