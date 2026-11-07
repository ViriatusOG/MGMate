<script setup lang="ts">
import '@/assets/logList.css'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatDate } from '@/utils/format'
import { dayKey, monthCells } from '@/utils/dayGroups'

export interface CalendarDay {
  /** A short figure for the day, such as "60 mi". */
  value: string
  count: number
  /** 0 to 1: how busy the day was against the busiest of the month, for its shade. */
  level: number
}

const props = defineProps<{
  month: Date
  /** By day key ("YYYY-MM-DD"). */
  days: Record<string, CalendarDay>
  selected: string | null
  canNext: boolean
}>()

const emit = defineEmits<{
  (e: 'prev'): void
  (e: 'next'): void
  (e: 'today'): void
  (e: 'select', key: string): void
}>()

const { t } = useI18n()

const title = computed(() => formatDate(props.month, { month: 'long', year: 'numeric' }))
const todayKey = dayKey(new Date())

const weekdays = computed(() =>
  // 5 January 2026 is a Monday.
  Array.from({ length: 7 }, (_, i) => formatDate(new Date(2026, 0, 5 + i), { weekday: 'narrow' })),
)

const cells = computed(() =>
  monthCells(props.month).map((day) => {
    if (day === null) return null
    const key = dayKey(new Date(props.month.getFullYear(), props.month.getMonth(), day))
    return { day, key, info: props.days[key] ?? null }
  }),
)
</script>

<template>
  <div class="month-cal">
    <div class="month-cal__head">
      <button
        type="button"
        class="month-cal__nav"
        :aria-label="t('calendar.previousMonth')"
        @click="emit('prev')"
      >
        <font-awesome-icon icon="chevron-left" />
      </button>
      <div class="month-cal__title">
        <strong>{{ title }}</strong>
        <button type="button" class="month-cal__today" @click="emit('today')">
          {{ t('calendar.jumpToToday') }}
        </button>
      </div>
      <button
        type="button"
        class="month-cal__nav"
        :aria-label="t('calendar.nextMonth')"
        :disabled="!canNext"
        @click="emit('next')"
      >
        <font-awesome-icon icon="chevron-right" />
      </button>
    </div>
    <div class="month-cal__grid" role="grid" :aria-label="title">
      <span
        v-for="(w, i) in weekdays"
        :key="`w${i}`"
        class="month-cal__weekday"
        aria-hidden="true"
        >{{ w }}</span
      >
      <template v-for="(cell, i) in cells" :key="cell?.key ?? `blank${i}`">
        <span v-if="!cell" class="month-cal__blank" />
        <button
          v-else
          type="button"
          class="month-cal__day"
          :class="{
            'month-cal__day--active': cell.info,
            'month-cal__day--selected': selected === cell.key,
            'month-cal__day--today': todayKey === cell.key,
          }"
          :style="cell.info ? { '--level': String(0.25 + cell.info.level * 0.75) } : undefined"
          :disabled="!cell.info"
          :title="cell.info ? `${cell.info.value} · ${cell.info.count}` : undefined"
          :aria-label="cell.info ? `${cell.day}: ${cell.info.value}` : String(cell.day)"
          @click="emit('select', cell.key)"
        >
          <span class="month-cal__num">{{ cell.day }}</span>
          <span v-if="cell.info" class="month-cal__value">{{ cell.info.value }}</span>
        </button>
      </template>
    </div>
    <slot />
  </div>
</template>
