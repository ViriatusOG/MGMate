/** One line of an OverviewSparkline. */
export interface SparkSeries {
  /** [time in ms, value] pairs, oldest first. */
  points: readonly (readonly [number, number])[]
  tone: 'success' | 'warning' | 'info'
  min?: number
  max?: number
  dashed?: boolean
  fill?: boolean
}
