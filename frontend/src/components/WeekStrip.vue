<script setup lang="ts">
import '@/assets/logList.css'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatDate } from '@/utils/format'
import { dayKey } from '@/utils/dayGroups'
import type { CalendarDay } from '@/components/MonthCalendar.vue'

// A phone's stand-in for the month calendar: one week of the month on show, with a dot under each
// day that has trips or charges, and a button that opens the month and the search when wanted.
const props = defineProps<{
  month: Date
  days: Record<string, CalendarDay>
  selected: string | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'select', key: string): void
  (e: 'toggle'): void
}>()

const { t } = useI18n()

// The week of the chosen day; else of today, in the month on show; else of its latest busy day.
const anchor = computed(() => {
  if (props.selected) return new Date(`${props.selected}T12:00:00`)
  const today = new Date()
  const m = props.month
  if (today.getFullYear() === m.getFullYear() && today.getMonth() === m.getMonth()) return today
  const keys = Object.keys(props.days).sort()
  const busy = keys[keys.length - 1]
  return busy ? new Date(`${busy}T12:00:00`) : new Date(m.getFullYear(), m.getMonth(), 1, 12)
})

const week = computed(() => {
  const a = anchor.value
  const monday = new Date(a.getFullYear(), a.getMonth(), a.getDate() - ((a.getDay() + 6) % 7), 12)
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + i, 12)
    const key = dayKey(d)
    return {
      key,
      day: d.getDate(),
      weekday: formatDate(d, { weekday: 'narrow' }),
      label: formatDate(d, { weekday: 'long', day: 'numeric', month: 'long' }),
      info: props.days[key] ?? null,
      inMonth: d.getMonth() === props.month.getMonth(),
    }
  })
})
</script>

<template>
  <div class="week-strip">
    <div class="week-strip__days" role="group" :aria-label="t('calendar.week')">
      <button
        v-for="d in week"
        :key="d.key"
        type="button"
        class="week-strip__day"
        :class="{
          'is-busy': d.info,
          'is-selected': selected === d.key,
          'is-outside': !d.inMonth,
        }"
        :disabled="!d.info"
        :aria-label="d.label"
        :aria-pressed="selected === d.key"
        @click="emit('select', d.key)"
      >
        <small>{{ d.weekday }}</small>
        <b>{{ d.day }}</b>
        <i v-if="d.info" aria-hidden="true" />
      </button>
    </div>
    <button
      type="button"
      class="btn btn-sm btn-outline-secondary week-strip__more"
      :aria-expanded="open"
      @click="emit('toggle')"
    >
      <font-awesome-icon :icon="open ? 'xmark' : 'calendar-days'" />
      {{ open ? t('calendar.close') : t('calendar.monthAndSearch') }}
    </button>
  </div>
</template>
