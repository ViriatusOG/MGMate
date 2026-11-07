import type { ChargeSession } from '@/services/chargesApi'

/** A charge has to cover this much of the battery for its reading to mean anything. */
export const MIN_SOC_SPAN = 20
/** Readings further than this from what the car says it holds are a bad sample, not a battery. */
const PLAUSIBLE = { min: 0.6, max: 1.2 }

/** One capacity reading: the energy a charge put in over the share of the battery it filled. */
export interface CapacityReading {
  at: string
  kwh: number
  socSpan: number
}

/**
 * The capacity each charge implies. Only charges with measured energy count: an estimate from
 * the state of charge would just hand back the capacity it was worked out from.
 */
export function capacityReadings(
  charges: readonly ChargeSession[],
  nominalKwh: number | null,
): CapacityReading[] {
  const readings: CapacityReading[] = []
  for (const c of charges) {
    if (c.isHidden || c.energyAddedKwh === null || c.energyAddedKwh <= 0) continue
    if (c.socStartPercent === null || c.socEndPercent === null) continue
    const span = c.socEndPercent - c.socStartPercent
    if (span < MIN_SOC_SPAN) continue
    const kwh = c.energyAddedKwh / (span / 100)
    if (nominalKwh && (kwh < nominalKwh * PLAUSIBLE.min || kwh > nominalKwh * PLAUSIBLE.max)) {
      continue
    }
    readings.push({ at: c.startedAt, kwh, socSpan: span })
  }
  return readings.sort((a, b) => Date.parse(a.at) - Date.parse(b.at))
}

export interface CapacityEstimate {
  /** Weighted by how much of the battery each charge covered: a bigger charge is a better sample. */
  kwh: number
  /** The weighted spread of the readings around it. */
  spread: number
  count: number
  /** The fitted line's change per year, in kWh; null with too few readings or too short a span. */
  trendPerYear: number | null
  /** The fitted line's value at each reading, for drawing it. */
  trend: number[]
}

const YEAR_MS = 365.25 * 24 * 3_600_000
/** The most recent readings the current estimate is made of. */
const RECENT = 10

export function estimateCapacity(readings: readonly CapacityReading[]): CapacityEstimate | null {
  if (readings.length === 0) return null
  const recent = readings.slice(-RECENT)
  const weight = recent.reduce((sum, r) => sum + r.socSpan, 0)
  const kwh = recent.reduce((sum, r) => sum + r.kwh * r.socSpan, 0) / weight
  const variance = recent.reduce((sum, r) => sum + r.socSpan * (r.kwh - kwh) ** 2, 0) / weight
  const spread = Math.sqrt(variance)

  // A weighted least-squares line through all readings, time in years.
  const t0 = Date.parse(readings[0]!.at)
  const xs = readings.map((r) => (Date.parse(r.at) - t0) / YEAR_MS)
  const ws = readings.map((r) => r.socSpan)
  const sw = ws.reduce((a, b) => a + b, 0)
  const mx = xs.reduce((s, x, i) => s + x * ws[i]!, 0) / sw
  const my = readings.reduce((s, r, i) => s + r.kwh * ws[i]!, 0) / sw
  let sxx = 0
  let sxy = 0
  xs.forEach((x, i) => {
    sxx += ws[i]! * (x - mx) ** 2
    sxy += ws[i]! * (x - mx) * (readings[i]!.kwh - my)
  })
  // A slope needs a few readings spread over at least a month to say anything.
  const slope = readings.length >= 4 && xs[xs.length - 1]! >= 1 / 12 && sxx > 0 ? sxy / sxx : null
  const trend = xs.map((x) => (slope === null ? my : my + slope * (x - mx)))
  return { kwh, spread, count: readings.length, trendPerYear: slope, trend }
}
