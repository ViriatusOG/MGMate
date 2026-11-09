import { request, requestJson } from '@/services/apiCore'

export type WarrantyStatus = 'unknown' | 'active' | 'endingSoon' | 'expired' | 'notCovered'

export interface WarrantyCover {
  id: number
  name: string
  notes: string | null
  /** 0 means the part is not covered. */
  months: number
  /** The odometer reading at which the cover ends, in km. */
  distanceKm: number | null
  featured: boolean
  sortOrder: number
  status: WarrantyStatus
  endsOn: string | null
  daysRemaining: number | null
  kmRemaining: number | null
  timeUsed: number | null
  distanceUsed: number | null
  endedBy: 'time' | 'distance' | null
}

export interface Warranty {
  registrationDate: string | null
  deliveryDate: string | null
  deliveryOdometerKm: number | null
  odometerKm: number | null
  covers: WarrantyCover[]
}

export interface OwnershipDatesRequest {
  /** A plain day, yyyy-mm-dd. */
  registrationDate: string | null
  deliveryDate: string | null
  deliveryOdometerKm: number | null
}

export interface WarrantyCoverRequest {
  name: string
  notes: string | null
  months: number
  distanceKm: number | null
  featured: boolean
}

export const warrantyApi = {
  get: (vin: string) => request<Warranty>(`/api/vehicles/${vin}/warranty`),
  setDates: (vin: string, body: OwnershipDatesRequest) =>
    requestJson<Warranty>(`/api/vehicles/${vin}/ownership-dates`, 'PUT', body),
  addCover: (vin: string, body: WarrantyCoverRequest) =>
    requestJson<Warranty>(`/api/vehicles/${vin}/warranty/covers`, 'POST', body),
  updateCover: (vin: string, id: number, body: WarrantyCoverRequest) =>
    requestJson<Warranty>(`/api/vehicles/${vin}/warranty/covers/${id}`, 'PUT', body),
  deleteCover: (vin: string, id: number) =>
    requestJson<Warranty>(`/api/vehicles/${vin}/warranty/covers/${id}`, 'DELETE'),
  reset: (vin: string) => requestJson<Warranty>(`/api/vehicles/${vin}/warranty/reset`, 'POST', {}),
}
