import { buildQuery, request, requestJson } from '@/services/apiCore'

/** Mirrors ChargeSession.NotesMaxLength. */
export const CHARGE_NOTES_MAX_LENGTH = 500

/** One saved charge: a plug-in, from the first reading that said charging to the last. */
export interface ChargeSession {
  id: number
  startedAt: string
  endedAt: string
  socStartPercent: number | null
  socEndPercent: number | null
  /** Battery power integrated over the charge; null when the car reported too little power. */
  energyAddedKwh: number | null
  maxPowerKw: number | null
  chargeType: 'AC' | 'DC' | null
  latitude: number | null
  longitude: number | null
  odometerKm: number | null
  stopReason: string | null
  socTargetPercent: number | null
  notes: string | null
  isHidden: boolean
}

export interface ChargeChartPoint {
  recordedAt: string
  powerKw: number | null
  socPercent: number | null
}

export const chargesApi = {
  log: (vin: string, from: string, to: string) =>
    request<ChargeSession[]>(`/api/vehicles/${vin}/charges${buildQuery({ from, to })}`),
  chart: (vin: string, id: number) =>
    request<ChargeChartPoint[]>(`/api/vehicles/${vin}/charges/${id}/chart`),
  setNotes: (vin: string, id: number, notes: string | null) =>
    requestJson<ChargeSession>(`/api/vehicles/${vin}/charges/${id}`, 'PUT', { notes }),
  setHidden: (vin: string, id: number, hidden: boolean) =>
    requestJson<ChargeSession>(`/api/vehicles/${vin}/charges/${id}/hidden`, 'PUT', { hidden }),
  mergeWithNext: (vin: string, id: number) =>
    requestJson<ChargeSession>(`/api/vehicles/${vin}/charges/${id}/merge-next`, 'POST'),
}
