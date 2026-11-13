import { ref } from 'vue'
import type { Plugin } from 'chart.js'

/**
 * Which point of a line chart the pointer or a finger is on, for something outside the chart to
 * follow, such as the car on the trip's map, with a dashed line down the chart at that point.
 * The index is null when nothing is pointed at.
 */
export function useChartScrub(lineColor: () => string) {
  const index = ref<number | null>(null)

  const plugin: Plugin<'line'> = {
    id: 'chartScrub',
    afterEvent(chart) {
      const active = chart.getActiveElements()
      const next = active.length > 0 ? active[0]!.index : null
      if (next !== index.value) index.value = next
    },
    afterDatasetsDraw(chart) {
      const active = chart.getActiveElements()
      if (active.length === 0) return
      const x = active[0]!.element.x
      const { top, bottom } = chart.chartArea
      const ctx = chart.ctx
      ctx.save()
      ctx.beginPath()
      ctx.setLineDash([3, 3])
      ctx.lineWidth = 1
      ctx.strokeStyle = lineColor()
      ctx.moveTo(x, top)
      ctx.lineTo(x, bottom)
      ctx.stroke()
      ctx.restore()
    },
    // A chart made anew, as for another trip, starts with nothing pointed at.
    afterInit() {
      index.value = null
    },
  }

  return { index, plugin }
}
