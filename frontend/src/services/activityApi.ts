import { buildQuery, request } from '@/services/apiCore'

export type ActivityKind =
  | 'locked'
  | 'unlocked'
  | 'doorOpened'
  | 'doorsClosed'
  | 'plugged'
  | 'unplugged'
  | 'chargingStarted'
  | 'chargingStopped'
  | 'climateOn'
  | 'climateOff'
  | 'driveStarted'
  | 'driveEnded'

export interface ActivityEvent {
  at: string
  kind: ActivityKind
  socPercent: number | null
}

/** A stretch with no reading from the car. */
export interface ActivityGap {
  from: string
  to: string
}

export interface ActivityHour {
  hour: string
  readings: number
}

export interface ActivityTimeline {
  events: ActivityEvent[]
  gaps: ActivityGap[]
  hours: ActivityHour[]
  readings: number
}

export const activityApi = {
  timeline: (vin: string, from: string, to: string) =>
    request<ActivityTimeline>(`/api/vehicles/${vin}/activity${buildQuery({ from, to })}`),
}
