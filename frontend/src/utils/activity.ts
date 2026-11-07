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
