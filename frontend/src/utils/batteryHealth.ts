import type { ChargeSession } from '@/services/chargesApi'

/** A charge has to cover this much of the battery for its reading to mean anything. */
export const MIN_SOC_SPAN = 20
/** Readings further than this from what the car says it holds are a bad sample, not a battery. */
const PLAUSIBLE = { min: 0.6, max: 1.2 }

/** A charge that ends at or above this has filled the battery, which recalibrates the car's charge level. */
export const FULL_SOC = 99
/**
 * How long a full charge keeps the charge level trustworthy. The car only knows its exact charge
 * level at the top: in between it counts what goes in and out, and that drifts (on an LFP pack, whose
 * voltage barely moves across the middle, most of all). A few points of drift on a 30% charge moves
 * a reading by 8%, enough to make a 64 kWh pack read 67.
 */
export const CALIBRATION_DAYS = 30
const DAY_MS = 86_400_000
/** Fewer calibrated readings than this make an early estimate. */
export const SETTLED_COUNT = 5

/** One capacity reading: the energy a charge put in over the share of the battery it filled. */
export interface CapacityReading {
  at: string
  kwh: number
  socSpan: number
  /** Whether the car had been charged to full in the {@link CALIBRATION_DAYS} before this charge. */
  calibrated: boolean
}

/** When each charge that filled the battery ended, oldest first. Hidden charges count: they still calibrated the car. */
function fullChargeEnds(charges: readonly ChargeSession[]): number[] {
  return charges
    .filter((c) => c.socEndPercent !== null && c.socEndPercent >= FULL_SOC)
    .map((c) => Date.parse(c.endedAt))
    .sort((a, b) => a - b)
}

/**
 * The capacity each charge implies. Only charges with measured energy count: an estimate from
 * the state of charge would just hand back the capacity it was worked out from.
 */
export function capacityReadings(
  charges: readonly ChargeSession[],
  nominalKwh: number | null,
): CapacityReading[] {
  const fulls = fullChargeEnds(charges)
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
    const start = Date.parse(c.startedAt)
    const calibrated = fulls.some((end) => end <= start && start - end <= CALIBRATION_DAYS * DAY_MS)
    readings.push({ at: c.startedAt, kwh, socSpan: span, calibrated })
  }
  return readings.sort((a, b) => Date.parse(a.at) - Date.parse(b.at))
}

export interface CapacityEstimate {
  /** Weighted by how much of the battery each charge covered: a bigger charge is a better sample. */
  kwh: number
  /** The weighted spread of the readings around it. */
  spread: number
  count: number
  /**
   * Whether the estimate is made of calibrated readings. When none are, it is the raw readings'
   * figure, to show as a first impression and not as the battery's health.
   */
  calibrated: boolean
  /** The fitted line's change per year, in kWh; null with too few readings or too short a span. */
  trendPerYear: number | null
  /** The fitted line's value at each reading the estimate is made of, for drawing it. */
  trend: number[]
}

const YEAR_MS = 365.25 * 24 * 3_600_000
/** The most recent readings the current estimate is made of. */
const RECENT = 10

/**
 * The capacity estimate. Once any reading is calibrated, only calibrated readings count: the ones
 * from before a full charge are the car's drifted charge level as much as the battery.
 */
export function estimateCapacity(all: readonly CapacityReading[]): CapacityEstimate | null {
  if (all.length === 0) return null
  const calibrated = all.some((r) => r.calibrated)
  const readings = calibrated ? all.filter((r) => r.calibrated) : all
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
  return { kwh, spread, count: readings.length, calibrated, trendPerYear: slope, trend }
}

/**
 * Health against new, in percent, as shown: never over 100. A healthy new pack often measures a
 * little over its rated size and each reading has a margin, so a figure above new is shown as 100
 * with a note rather than as a battery that grew. Null until calibrated or without a pack size.
 */
export function healthPercent(
  estimate: CapacityEstimate | null,
  nominalKwh: number | null,
): number | null {
  if (!estimate?.calibrated || !nominalKwh) return null
  return Math.min(100, (estimate.kwh / nominalKwh) * 100)
}
