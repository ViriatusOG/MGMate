import type { LatLng } from './geo'

/** A fix with the time it was taken, as the route's points carry them. */
export interface TimedFix {
  recordedAt: string
  latitude: number
  longitude: number
}

/** Where the car was at a moment of the drive, which way it was heading, and the route after it. */
export interface RoutePosition {
  at: LatLng
  /** Compass bearing in degrees, 0 north and 90 east; null when the car had not moved yet. */
  heading: number | null
  /** The rest of the route from that spot on, to be drawn as not yet driven. */
  ahead: LatLng[]
}

/** Degrees from north, clockwise, of the line from one point to the next. */
export function bearing(from: LatLng, to: LatLng): number {
  const rad = Math.PI / 180
  const [lat1, lon1] = [from[0] * rad, from[1] * rad]
  const [lat2, lon2] = [to[0] * rad, to[1] * rad]
  const y = Math.sin(lon2 - lon1) * Math.cos(lat2)
  const x =
    Math.cos(lat1) * Math.sin(lat2) - Math.sin(lat1) * Math.cos(lat2) * Math.cos(lon2 - lon1)
  return (((Math.atan2(y, x) / rad) % 360) + 360) % 360
}

const same = (a: LatLng, b: LatLng) => a[0] === b[0] && a[1] === b[1]

/**
 * Where the car was at `time` (ms), read off the route's fixes: between two fixes it is put
 * the same share of the way along as the time is between them. Before the first fix it is at the
 * start and after the last at the end. Null without fixes.
 */
export function positionAt(fixes: readonly TimedFix[], time: number): RoutePosition | null {
  if (fixes.length === 0) return null
  const times = fixes.map((f) => Date.parse(f.recordedAt))
  const ll = (i: number): LatLng => [fixes[i]!.latitude, fixes[i]!.longitude]

  // The last fix at or before the time, by halving.
  let lo = 0
  let hi = fixes.length - 1
  if (time <= times[0]!) hi = 0
  else if (time >= times[hi]!) lo = hi
  else {
    while (hi - lo > 1) {
      const mid = (lo + hi) >> 1
      if (times[mid]! <= time) lo = mid
      else hi = mid
    }
  }
  const i = Math.min(lo, hi)
  const j = Math.max(lo, hi)
  const span = times[j]! - times[i]!
  const share = span > 0 ? Math.min(1, Math.max(0, (time - times[i]!) / span)) : 0
  const a = ll(i)
  const b = ll(j)
  const at: LatLng = [a[0] + (b[0] - a[0]) * share, a[1] + (b[1] - a[1]) * share]

  // The heading is that of the stretch it is on, or of the nearest stretch it moved along.
  let heading: number | null = null
  for (let k = i; k < fixes.length - 1 && heading === null; k++) {
    if (!same(ll(k), ll(k + 1))) heading = bearing(ll(k), ll(k + 1))
  }
  for (let k = i; k > 0 && heading === null; k--) {
    if (!same(ll(k - 1), ll(k))) heading = bearing(ll(k - 1), ll(k))
  }

  const ahead: LatLng[] = [at]
  for (let k = j > i ? j : i + 1; k < fixes.length; k++) {
    ahead.push(ll(k))
  }
  return { at, heading, ahead }
}
