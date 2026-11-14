<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MaintenanceItem } from '@/services/maintenanceApi'
import { dueProgress, dueStatusVariant, formatIntervalSummary } from '@/utils/maintenance'
import { ODOMETER_FORMAT } from '@/utils/units'
import { formatDate } from '@/utils/format'
import { formatTimeSpan, utcDay } from '@/utils/timeSpan'
import { todayUtc } from '@/utils/warranty'
import { useUnits } from '@/composables/useUnits'

const props = defineProps<{
  item: MaintenanceItem
  /** One row in a list: the name, what is left and the status, for a phone. */
  compact?: boolean
}>()
defineEmits<{ (e: 'open'): void }>()

const { t } = useI18n()
const units = useUnits()

const variant = computed(() => dueStatusVariant(props.item.dueStatus))
const progress = computed(() => dueProgress(props.item))

function distance(km: number): string {
  return units.value.format('distance', km, ODOMETER_FORMAT) ?? '-'
}

// "4 mo 29 d left" from the due date; "due today" and an item without a date keep the day count.
function timeText(remaining: number): string {
  const days = Math.round(remaining)
  const due = props.item.nextDueDate
  if (days === 0 || !due)
    return days < 0 ? t('maintenance.overDays', -days) : t('maintenance.leftDays', days)
  return days < 0
    ? t('maintenance.over', { amount: formatTimeSpan(t, utcDay(due), todayUtc()) })
    : t('maintenance.left', { amount: formatTimeSpan(t, todayUtc(), utcDay(due)) })
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
      text: timeText(time.remaining),
    })
  return out
})

// A row has one line for it: what is left on each count, or failing that how often it comes round.
const compactLine = computed(() =>
  bars.value.length > 0
    ? bars.value.map((bar) => bar.text).join(' · ')
    : formatIntervalSummary(props.item, t, units.value),
)

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
  const { lastServiceDate, lastServiceOdometerKm, countingFrom, countingFromDate } = props.item
  if (countingFrom === 'registration' && countingFromDate)
    return t('maintenance.countingFromRegistration', {
      when: formatDate(countingFromDate, { day: 'numeric', month: 'short', year: 'numeric' }),
    })
  if (!lastServiceDate && lastServiceOdometerKm == null) return t('maintenance.neverDone')
  const parts: string[] = []
  if (lastServiceDate)
    parts.push(formatDate(lastServiceDate, { day: 'numeric', month: 'short', year: 'numeric' }))
  if (lastServiceOdometerKm != null) parts.push(distance(lastServiceOdometerKm))
  return t('maintenance.lastDone', { when: parts.join(' · ') })
})
</script>

<template>
  <button
    v-if="compact"
    type="button"
    class="due-row"
    :class="`due-card--${variant}`"
    @click="$emit('open')"
  >
    <span class="due-row__dot" aria-hidden="true" />
    <span class="due-row__main">
      <span class="due-card__name">{{ item.name }}</span>
      <span class="due-card__interval">{{ compactLine }}</span>
    </span>
    <span class="due-card__badge">{{ t(`maintenance.status.${item.dueStatus}`) }}</span>
  </button>
  <button
    v-else
    type="button"
    class="due-card"
    :class="`due-card--${variant}`"
    @click="$emit('open')"
  >
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
    <span v-if="item.countingFrom" class="due-card__meta">{{ lastLine }}</span>
    <span v-else class="due-card__start">{{ t('maintenance.noStartPoint') }} →</span>
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

.due-row {
  --tone: var(--color-success, #22c55e);

  display: flex;
  gap: 0.75rem;
  align-items: center;
  width: 100%;
  min-height: 52px;
  padding: 0.55rem 0.9rem;
  font: inherit;
  color: inherit;
  text-align: left;
  cursor: pointer;
  background: none;
  border: 0;
}

.due-row__dot {
  flex: none;
  width: 0.6rem;
  height: 0.6rem;
  background: var(--tone);
  border-radius: 50%;
}

.due-row__main {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 0;
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

.due-card__start {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--color-primary, #3b82f6);
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
