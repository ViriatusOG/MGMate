<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatTime } from '@/utils/format'
import { niceTop, timeTicks, type ChartSeries } from './chart'

// A small chart for an overview card: each line's scale on its own side (power on the left,
// charge level on the right), times along the bottom, and a legend naming the lines. Drawn in
// real pixels from the box's measured size, so the labels never stretch.

const props = defineProps<{
  series: readonly ChartSeries[]
  /** The time span drawn; defaults to the span of the points. */
  from?: number
  to?: number
  /** Write "Now" for the end rather than its time. */
  endsNow?: boolean
  label: string
}>()

const { t } = useI18n()

const box = ref<HTMLElement | null>(null)
const size = ref({ w: 300, h: 120 })
let observer: ResizeObserver | null = null

onMounted(() => {
  const el = box.value
  if (!el || typeof ResizeObserver === 'undefined') return
  observer = new ResizeObserver(([entry]) => {
    const r = entry?.contentRect
    if (r && r.width > 0 && r.height > 0) size.value = { w: r.width, h: r.height }
  })
  observer.observe(el)
})
onBeforeUnmount(() => observer?.disconnect())

const TOP = 18
const BOTTOM = 18
const SIDE = 30

const drawn = computed(() => props.series.filter((s) => s.points.length > 0))
const sideOf = (s: ChartSeries, i: number) => s.axis ?? (i === 0 ? 'left' : 'right')
const hasRight = computed(() => drawn.value.some((s, i) => sideOf(s, i) === 'right'))

const plot = computed(() => {
  const { w, h } = size.value
  const x0 = SIDE
  const x1 = w - (hasRight.value ? SIDE : 6)
  return { x0, x1, y0: TOP, y1: Math.max(TOP + 10, h - BOTTOM), w, h }
})

const span = computed(() => {
  const times = drawn.value.flatMap((s) => s.points.map((p) => p[0]))
  const from = props.from ?? Math.min(...times)
  const to = props.to ?? Math.max(...times)
  return { from, to: to > from ? to : from + 1 }
})

const xOf = (time: number) => {
  const { x0, x1 } = plot.value
  const { from, to } = span.value
  return x0 + Math.max(0, Math.min(1, (time - from) / (to - from))) * (x1 - x0)
}

const lines = computed(() =>
  drawn.value.map((s, i) => {
    const values = s.points.map((p) => p[1])
    const min = s.min ?? 0
    const max = s.max ?? niceTop(Math.max(...values))
    const top = max > min ? max : min + 1
    const { y0, y1 } = plot.value
    const yOf = (v: number) => y1 - Math.max(0, Math.min(1, (v - min) / (top - min))) * (y1 - y0)
    const xy = s.points.map(([time, v]) => [xOf(time), yOf(v)] as const)
    // A single reading draws as a flat line across the box.
    const pts = xy.length === 1 ? [[plot.value.x0, xy[0]![1]] as const, xy[0]!] : xy
    const d = pts.map(([x, y], k) => `${k ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`).join(' ')
    const first = pts[0]!
    const last = pts[pts.length - 1]!
    let peak = null
    if (s.peak) {
      const k = values.indexOf(Math.max(...values))
      const [x, y] = xy[k]!
      peak = { x, y, text: s.peak(values[k]!), end: x > (plot.value.x0 + plot.value.x1) / 2 }
    }
    return {
      tone: s.tone,
      dashed: !!s.dashed,
      side: sideOf(s, i),
      unit: s.unit,
      ticks: [min, (min + top) / 2, top].map((v) => ({ y: yOf(v), text: tickText(v) })),
      d,
      area: s.fill ? `${d} L${last[0].toFixed(1)} ${y1} L${first[0].toFixed(1)} ${y1} Z` : null,
      peak,
    }
  }),
)

function tickText(v: number): string {
  return Number.isInteger(v) ? String(v) : v.toFixed(1)
}

const left = computed(() => lines.value.find((l) => l.side === 'left') ?? null)
const right = computed(() => lines.value.find((l) => l.side === 'right') ?? null)
/** The gridlines follow the left scale, or the right when there is no left. */
const grid = computed(() => (left.value ?? right.value)?.ticks ?? [])

const xTicks = computed(() => {
  const { x0, x1 } = plot.value
  const { from, to } = span.value
  const ticks = timeTicks(from, to, x1 - x0)
  return ticks.map((time, i) => ({
    x: xOf(time),
    text:
      i === ticks.length - 1 && props.endsNow
        ? t('overview.chart.now')
        : formatTime(new Date(time), { hour: '2-digit', minute: '2-digit' }),
    anchor: i === ticks.length - 1 ? 'end' : xOf(time) - x0 < 20 ? 'start' : 'middle',
  }))
})
</script>

<template>
  <div class="ov-chart">
    <ul class="ov-chart__legend">
      <li v-for="(s, i) in drawn" :key="i" :class="`is-${s.tone}`">
        <i :class="{ 'is-dashed': s.dashed }" aria-hidden="true" />{{ s.name }}
      </li>
    </ul>
    <div ref="box" class="ov-chart__box">
      <svg
        class="ov-chart__svg"
        :width="plot.w"
        :height="plot.h"
        :viewBox="`0 0 ${plot.w} ${plot.h}`"
        role="img"
        :aria-label="label"
      >
        <line
          v-for="(g, i) in grid"
          :key="`g${i}`"
          class="ov-chart__grid"
          :class="{ 'is-base': i === 0 }"
          :x1="plot.x0"
          :x2="plot.x1"
          :y1="g.y"
          :y2="g.y"
        />
        <g v-if="left" class="ov-chart__axis" :class="right ? `is-${left.tone}` : null">
          <text :x="plot.x0 - 5" :y="10" text-anchor="end" class="ov-chart__unit">
            {{ left.unit }}
          </text>
          <text
            v-for="(tk, i) in left.ticks"
            :key="i"
            :x="plot.x0 - 5"
            :y="tk.y + 3.5"
            text-anchor="end"
          >
            {{ tk.text }}
          </text>
        </g>
        <g v-if="right" class="ov-chart__axis" :class="`is-${right.tone}`">
          <text :x="plot.x1 + 5" :y="10" class="ov-chart__unit">{{ right.unit }}</text>
          <text v-for="(tk, i) in right.ticks" :key="i" :x="plot.x1 + 5" :y="tk.y + 3.5">
            {{ tk.text }}
          </text>
        </g>
        <g class="ov-chart__axis">
          <template v-for="(tk, i) in xTicks" :key="i">
            <line class="ov-chart__tick" :x1="tk.x" :x2="tk.x" :y1="plot.y1" :y2="plot.y1 + 4" />
            <text :x="tk.x" :y="plot.h - 3" :text-anchor="tk.anchor">{{ tk.text }}</text>
          </template>
        </g>
        <template v-for="(line, i) in lines" :key="i">
          <path v-if="line.area" :d="line.area" class="ov-chart__area" :class="`is-${line.tone}`" />
          <path
            :d="line.d"
            class="ov-chart__line"
            :class="[`is-${line.tone}`, { 'is-dashed': line.dashed }]"
          />
        </template>
        <template v-for="(line, i) in lines" :key="`p${i}`">
          <g v-if="line.peak" class="ov-chart__peak" :class="`is-${line.tone}`">
            <circle :cx="line.peak.x" :cy="line.peak.y" r="3" />
            <text
              :x="line.peak.x + (line.peak.end ? -6 : 6)"
              :y="Math.max(9, line.peak.y - 5)"
              :text-anchor="line.peak.end ? 'end' : 'start'"
            >
              {{ line.peak.text }}
            </text>
          </g>
        </template>
      </svg>
    </div>
  </div>
</template>
