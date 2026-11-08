<script setup lang="ts">
import { computed } from 'vue'
import type { SparkSeries } from './sparkline'

// A small line for an overview card: no axes, and the area under a line shaded when asked. Each
// line has its own vertical scale, so power and charge level can share one box; time is shared.

const props = defineProps<{
  series: readonly SparkSeries[]
  /** The time span drawn; defaults to the span of the points. */
  from?: number
  to?: number
  label: string
}>()

const W = 300
const H = 64
const PAD = 4

const span = computed(() => {
  const times = props.series.flatMap((s) => s.points.map((p) => p[0]))
  const from = props.from ?? Math.min(...times)
  const to = props.to ?? Math.max(...times)
  return { from, to: to > from ? to : from + 1 }
})

const lines = computed(() =>
  props.series
    .filter((s) => s.points.length > 0)
    .map((s) => {
      const values = s.points.map((p) => p[1])
      const min = s.min ?? Math.min(...values)
      let max = s.max ?? Math.max(...values)
      if (max <= min) max = min + 1
      const { from, to } = span.value
      const xy = s.points.map(([t, v]) => {
        const x = ((t - from) / (to - from)) * W
        const y = PAD + (1 - (v - min) / (max - min)) * (H - 2 * PAD)
        return [Math.max(0, Math.min(W, x)), Math.max(0, Math.min(H, y))] as const
      })
      // A single reading draws as a flat line across the box.
      const drawn = xy.length === 1 ? [[0, xy[0]![1]] as const, xy[0]!] : xy
      const d = drawn
        .map(([x, y], i) => `${i ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`)
        .join(' ')
      const first = drawn[0]!
      const last = drawn[drawn.length - 1]!
      return {
        tone: s.tone,
        dashed: !!s.dashed,
        d,
        area: s.fill ? `${d} L${last[0].toFixed(1)} ${H} L${first[0].toFixed(1)} ${H} Z` : null,
      }
    }),
)
</script>

<template>
  <svg
    class="ov-spark"
    :viewBox="`0 0 ${W} ${H}`"
    preserveAspectRatio="none"
    role="img"
    :aria-label="label"
  >
    <template v-for="(line, i) in lines" :key="i">
      <path v-if="line.area" :d="line.area" class="ov-spark__area" :class="`is-${line.tone}`" />
      <path
        :d="line.d"
        class="ov-spark__line"
        :class="[`is-${line.tone}`, { 'is-dashed': line.dashed }]"
        vector-effect="non-scaling-stroke"
      />
    </template>
  </svg>
</template>
