import type { TripTelemetryPoint } from '@/services/tripLogApi'
import type { TripPoint } from '@/services/vehicleApi'

/** A reading further than this from the one before says nothing about the time in between. */
const MAX_GAP_MS = 10 * 60_000
/** Elevation from GPS wanders by a metre or two at rest; smaller changes than this are noise. */
const CLIMB_STEP_M = 3

export interface TripTelemetrySummary {
  /** Energy out of the battery, from its power over the trip. */
  usedKwh: number | null
  /** Energy back into it while driving: regenerative braking. */
  regenKwh: number | null
  climbM: number | null
  descentM: number | null
  outsideStart: number | null
  outsideEnd: number | null
  climateMinutes: number | null
  /** Minutes moving and standing still inside the trip; a gap in the readings counts as neither. */
  drivingMinutes: number | null
  stoppedMinutes: number | null
  /** The speed half the moving readings stayed below: a town drive's usual pace, unlike the mean. */
  medianSpeedKmh: number | null
}

/** Below this a reading is standing still: a crawl in a queue still counts as moving. */
const STOPPED_KMH = 2

function time(p: TripTelemetryPoint): number {
  return Date.parse(p.recordedAt)
}

/**
 * The battery power integrated both ways. Which sign means discharging differs between cars, so
 * the larger total is taken as what driving used and the smaller as what braking put back.
 */
function powerTotals(points: readonly TripTelemetryPoint[]) {
  const withPower = points.filter((p) => p.powerKw !== null)
  if (withPower.length < 3) return { used: null, regen: null }
  let positive = 0
  let negative = 0
  for (let i = 1; i < withPower.length; i++) {
    const a = withPower[i - 1]!
    const b = withPower[i]!
    const ms = time(b) - time(a)
    if (ms <= 0 || ms > MAX_GAP_MS) continue
    const hours = ms / 3_600_000
    // Each half of the step counts on the side of its own reading, so a step that crosses zero
    // is not cancelled out against itself.
    for (const kw of [a.powerKw!, b.powerKw!]) {
      const kwh = (kw * hours) / 2
      if (kwh > 0) positive += kwh
      else negative -= kwh
    }
  }
  const used = Math.max(positive, negative)
  const regen = Math.min(positive, negative)
  return used < 0.01 ? { used: null, regen: null } : { used, regen }
}

function climb(points: readonly TripTelemetryPoint[]) {
  const elevations = points.map((p) => p.elevation).filter((e): e is number => e !== null)
  if (elevations.length < 2) return { up: null, down: null }
  let reference = elevations[0]!
  let up = 0
  let down = 0
  for (const e of elevations) {
    if (e - reference >= CLIMB_STEP_M) {
      up += e - reference
      reference = e
    } else if (reference - e >= CLIMB_STEP_M) {
      down += reference - e
      reference = e
    }
  }
  return { up, down }
}

function climateMinutes(points: readonly TripTelemetryPoint[]): number | null {
  const known = points.filter((p) => p.climateOn !== null)
  if (known.length === 0) return null
  let ms = 0
  for (let i = 1; i < known.length; i++) {
    const gap = time(known[i]!) - time(known[i - 1]!)
    if (known[i - 1]!.climateOn && gap > 0 && gap <= MAX_GAP_MS) ms += gap
  }
  return ms / 60_000
}

function motion(points: readonly TripTelemetryPoint[]) {
  const known = points.filter((p) => p.speed !== null)
  if (known.length < 2) return { driving: null, stopped: null, median: null }
  let driving = 0
  let stopped = 0
  for (let i = 1; i < known.length; i++) {
    const gap = time(known[i]!) - time(known[i - 1]!)
    if (gap <= 0 || gap > MAX_GAP_MS) continue
    // A step is moving when either end of it was: the car pulls away or comes to rest within it.
    if (known[i - 1]!.speed! >= STOPPED_KMH || known[i]!.speed! >= STOPPED_KMH) driving += gap
    else stopped += gap
  }
  const moving = known
    .map((p) => p.speed!)
    .filter((s) => s >= STOPPED_KMH)
    .sort((a, b) => a - b)
  const mid = moving.length >> 1
  const median =
    moving.length === 0
      ? null
      : moving.length % 2
        ? moving[mid]!
        : (moving[mid - 1]! + moving[mid]!) / 2
  return { driving: driving / 60_000, stopped: stopped / 60_000, median }
}

export function summarizeTripTelemetry(
  points: readonly TripTelemetryPoint[],
): TripTelemetrySummary {
  const { used, regen } = powerTotals(points)
  const { up, down } = climb(points)
  const temps = points.map((p) => p.outsideTemperature).filter((v): v is number => v !== null)
  const { driving, stopped, median } = motion(points)
  return {
    usedKwh: used,
    regenKwh: regen,
    climbM: up,
    descentM: down,
    outsideStart: temps[0] ?? null,
    outsideEnd: temps[temps.length - 1] ?? null,
    climateMinutes: climateMinutes(points),
    drivingMinutes: driving,
    stoppedMinutes: stopped,
    medianSpeedKmh: median,
  }
}

/** Positive power as driving, whichever sign this car uses: so regen always reads below zero. */
export function drivingPowerSign(points: readonly TripTelemetryPoint[]): 1 | -1 {
  let sum = 0
  for (const p of points) sum += p.powerKw ?? 0
  return sum < 0 ? -1 : 1
}

function escapeXml(text: string): string {
  return text.replace(/[<>&'"]/g, (c) => `&#${c.charCodeAt(0)};`)
}

/** The elevation reported nearest a moment, within two minutes of it. */
function elevationAt(at: number, telemetry: readonly TripTelemetryPoint[]): number | null {
  let best: number | null = null
  let bestGap = 2 * 60_000
  for (const p of telemetry) {
    if (p.elevation === null) continue
    const gap = Math.abs(time(p) - at)
    if (gap <= bestGap) {
      best = p.elevation
      bestGap = gap
    }
  }
  return best
}

/** The route as a GPX 1.1 track, with each fix's time and, where the car reported it, elevation. */
export function buildGpx(
  name: string,
  points: readonly TripPoint[],
  telemetry: readonly TripTelemetryPoint[] = [],
): string {
  const fixes = points.map((p) => {
    const elevation = elevationAt(Date.parse(p.recordedAt), telemetry)
    const ele = elevation === null ? '' : `<ele>${elevation.toFixed(1)}</ele>`
    return `      <trkpt lat="${p.latitude.toFixed(6)}" lon="${p.longitude.toFixed(6)}">${ele}<time>${new Date(p.recordedAt).toISOString()}</time></trkpt>`
  })
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<gpx version="1.1" creator="MGMate" xmlns="http://www.topografix.com/GPX/1/1">',
    '  <trk>',
    `    <name>${escapeXml(name)}</name>`,
    '    <trkseg>',
    ...fixes,
    '    </trkseg>',
    '  </trk>',
    '</gpx>',
    '',
  ].join('\n')
}

/** Hands the browser a file to save. */
export function downloadText(filename: string, text: string, type: string) {
  const url = URL.createObjectURL(new Blob([text], { type }))
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  URL.revokeObjectURL(url)
}
