/** One line of an OverviewChart. */
export interface ChartSeries {
  /** [time in ms, value] pairs, oldest first. */
  points: readonly (readonly [number, number])[]
  tone: 'success' | 'warning' | 'info'
  /** What the line is, for the legend: "Power (kW)". */
  name: string
  /** The unit written above its axis: "kW" or "%". */
  unit: string
  /** The side its scale is drawn on; the first line takes the left. */
  axis?: 'left' | 'right'
  min?: number
  max?: number
  dashed?: boolean
  fill?: boolean
  /** Mark the highest reading with its value, written with this. */
  peak?: (value: number) => string
}

const STEPS = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10]

/**
 * The top of an axis with three ticks (0, half, top) that holds `max`: a round number, so 96.9 kW
 * reads 0, 50, 100 and an 11 kW home charge 0, 6, 12.
 */
export function niceTop(max: number): number {
  if (!(max > 0)) return 1
  const half = max / 2
  const scale = 10 ** Math.floor(Math.log10(half))
  const step = STEPS.find((s) => s * scale >= half) ?? 10
  return 2 * step * scale
}

const HOUR = 3_600_000
const MINUTE = 60_000
const MINUTE_STEPS = [1, 2, 5, 10, 15, 20, 30, 60, 120, 180, 360, 720].map((m) => m * MINUTE)

/**
 * The times written along the bottom. A span of hours gets whole clock hours; a short one (a
 * charge) starts at its start and steps on from there. The end is always written; ticks that
 * would crowd a neighbour are left out.
 */
export function timeTicks(from: number, to: number, width: number, minGap = 44): number[] {
  const span = to - from
  if (!(span > 0) || width <= 0) return []
  const wanted = Math.max(1, Math.floor(width / (minGap * 1.6)))
  const step = MINUTE_STEPS.find((s) => span / s <= wanted) ?? 24 * HOUR
  const ticks: number[] = []
  if (span >= 6 * HOUR) {
    // Whole local hours, so the labels read 12:00, 18:00 rather than 09:34, 15:34.
    const start = new Date(from)
    start.setMinutes(0, 0, 0)
    let t = start.getTime()
    while (t <= from) t += HOUR
    while (t < to) {
      if (new Date(t).getHours() % Math.max(1, Math.round(step / HOUR)) === 0) ticks.push(t)
      t += HOUR
    }
  } else {
    for (let t = from; t < to; t += step) ticks.push(t)
  }
  // Keep the end, and walk back from it dropping what crowds.
  const px = (t: number) => ((t - from) / span) * width
  const kept: number[] = [to]
  for (let i = ticks.length - 1; i >= 0; i--) {
    if (px(kept[0]!) - px(ticks[i]!) >= minGap) kept.unshift(ticks[i]!)
  }
  return kept
}
