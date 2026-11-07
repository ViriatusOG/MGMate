<script setup lang="ts">
import '@/assets/logList.css'
import { computed } from 'vue'

export interface DonutSlice {
  key: string
  label: string
  count: number
  note: string
  color: string
}

// A split drawn as a ring, with each part's count and figures beside it.
const props = defineProps<{ title: string; centre: string; slices: DonutSlice[] }>()

const total = computed(() => props.slices.reduce((s, x) => s + x.count, 0))

const ring = computed(() => {
  if (total.value === 0) return 'var(--color-surface-2)'
  let at = 0
  const stops = props.slices.map((s) => {
    const from = at
    at += (s.count / total.value) * 360
    return `${s.color} ${from}deg ${at}deg`
  })
  return `conic-gradient(${stops.join(', ')})`
})
</script>

<template>
  <section class="donut-card">
    <h2 class="donut-card__title">{{ title }}</h2>
    <div class="donut-card__body">
      <div class="donut-card__ring" :style="{ background: ring }" role="img" :aria-label="title">
        <div class="donut-card__hole">
          <span class="text-xs text-muted">{{ centre }}</span>
          <strong>{{ total }}</strong>
        </div>
      </div>
      <ul class="donut-card__legend">
        <li v-for="s in slices" :key="s.key">
          <span class="donut-card__dot" :style="{ background: s.color }" />
          <strong>{{ s.count }}</strong> {{ s.label }}
          <span class="donut-card__note text-xs text-muted">{{ s.note }}</span>
        </li>
      </ul>
    </div>
  </section>
</template>
