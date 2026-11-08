import type { ActivityEvent, ActivityGap, ActivityKind } from '@/services/activityApi'

export const ACTIVITY_ICONS: Record<ActivityKind, string> = {
  locked: 'lock',
  unlocked: 'lock-open',
  doorOpened: 'door-open',
  doorsClosed: 'door-closed',
  plugged: 'plug',
  unplugged: 'plug',
  chargingStarted: 'charging-station',
  chargingStopped: 'charging-station',
  climateOn: 'fan',
  climateOff: 'fan',
  driveStarted: 'car-side',
  driveEnded: 'square-parking',
}

export type ActivityRow =
  | { type: 'event'; at: string; event: ActivityEvent }
  | { type: 'gap'; at: string; gap: ActivityGap }

/** Events and quiet stretches in one list, newest first, a gap placed where it began. */
export function activityRows(
  events: readonly ActivityEvent[],
  gaps: readonly ActivityGap[],
): ActivityRow[] {
  const rows: ActivityRow[] = [
    ...events.map((event) => ({ type: 'event' as const, at: event.at, event })),
    ...gaps.map((gap) => ({ type: 'gap' as const, at: gap.from, gap })),
  ]
  return rows.sort((a, b) => Date.parse(b.at) - Date.parse(a.at))
}

/** The share of a period the car was reporting, from its quiet stretches. */
export function onlineShare(gaps: readonly ActivityGap[], from: Date, to: Date): number {
  const end = Math.min(to.getTime(), Date.now())
  const span = end - from.getTime()
  if (span <= 0) return 1
  const quiet = gaps.reduce((sum, g) => sum + (Date.parse(g.to) - Date.parse(g.from)), 0)
  return Math.max(0, Math.min(1, 1 - quiet / span))
}

export interface ActivitySpan {
  from: number
  to: number
}

/**
 * Stretches between a start and an end event, clipped to the period. A start with no end runs
 * to the end of the period (or now); an end with no start began when the period did.
 */
export function activitySpans(
  events: readonly ActivityEvent[],
  start: ActivityKind,
  end: ActivityKind,
  from: Date,
  to: Date,
): ActivitySpan[] {
  const periodStart = from.getTime()
  const periodEnd = Math.min(to.getTime(), Date.now())
  const sorted = events
    .filter((e) => e.kind === start || e.kind === end)
    .map((e) => ({ kind: e.kind, at: Date.parse(e.at) }))
    .sort((a, b) => a.at - b.at)
  const spans: ActivitySpan[] = []
  let open: number | null = null
  let seenAny = false
  for (const e of sorted) {
    if (e.kind === start) {
      if (open === null) open = e.at
    } else if (open !== null) {
      spans.push({ from: open, to: e.at })
      open = null
    } else if (!seenAny) {
      spans.push({ from: periodStart, to: e.at })
    }
    seenAny = true
  }
  if (open !== null && periodEnd > open) spans.push({ from: open, to: periodEnd })
  return spans
    .map((s) => ({ from: Math.max(s.from, periodStart), to: Math.min(s.to, periodEnd) }))
    .filter((s) => s.to > s.from)
}

export function spansTotalMs(spans: readonly ActivitySpan[]): number {
  return spans.reduce((sum, s) => sum + (s.to - s.from), 0)
}

/** How often the car came back after a quiet stretch within the period. */
export function timesWoken(gaps: readonly ActivityGap[], from: Date, to: Date): number {
  return gaps.filter((g) => {
    const end = Date.parse(g.to)
    return end > from.getTime() && end < to.getTime() && end <= Date.now()
  }).length
}

export type ActivityTone = 'drive' | 'charge' | 'climate' | 'access'

export function activityTone(kind: ActivityKind): ActivityTone {
  if (kind === 'driveStarted' || kind === 'driveEnded') return 'drive'
  if (kind.startsWith('charging') || kind === 'plugged' || kind === 'unplugged') return 'charge'
  if (kind.startsWith('climate')) return 'climate'
  return 'access'
}
