import { describe, expect, it, vi } from 'vitest'
import type { Chart } from 'chart.js'
import { useChartScrub } from '../useChartScrub'

const chartPointedAt = (index: number | null) => {
  const ctx = {
    save: vi.fn<() => void>(),
    restore: vi.fn<() => void>(),
    beginPath: vi.fn<() => void>(),
    setLineDash: vi.fn<(segments: number[]) => void>(),
    moveTo: vi.fn<(...args: number[]) => void>(),
    lineTo: vi.fn<(...args: number[]) => void>(),
    stroke: vi.fn<() => void>(),
    lineWidth: 0,
    strokeStyle: '',
  }
  const chart = {
    getActiveElements: () => (index === null ? [] : [{ index, element: { x: 120 } }]),
    chartArea: { top: 10, bottom: 300 },
    ctx,
  }
  return { chart: chart as unknown as Chart<'line'>, ctx }
}

describe('scrubbing a chart', () => {
  it('follows the point the pointer is on, and lets go when it leaves', () => {
    const scrub = useChartScrub(() => '#999')
    const call = (hook: 'afterEvent' | 'afterDatasetsDraw', chart: Chart<'line'>) =>
      (scrub.plugin[hook] as (c: Chart<'line'>, a: object, o: object) => void)(chart, {}, {})

    call('afterEvent', chartPointedAt(42).chart)
    expect(scrub.index.value).toBe(42)

    const { chart, ctx } = chartPointedAt(42)
    call('afterDatasetsDraw', chart)
    expect(ctx.moveTo).toHaveBeenCalledWith(120, 10)
    expect(ctx.lineTo).toHaveBeenCalledWith(120, 300)
    expect(ctx.strokeStyle).toBe('#999')

    call('afterEvent', chartPointedAt(null).chart)
    expect(scrub.index.value).toBeNull()
  })
})
