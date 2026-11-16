import { request } from '@/services/apiCore'

/** The code the DVLA endpoints answer with while the deployment has no DVLA key of its own. */
export const DVLA_DISABLED_CODE = 'dvla.disabled'

/** What the DVLA last said about the plate. */
export interface DvlaInfo {
  /** False when the DVLA knows no vehicle by the plate. */
  found: boolean
  /** The DVLA's own words: "Taxed", "Untaxed", "SORN", "Not Taxed for on Road Use". */
  taxStatus: string | null
  /** "YYYY-MM-DD" on the UK calendar. */
  taxDueDate: string | null
  /** Counted by the API on the UK calendar: 0 on the day itself, negative once it passed. */
  taxDaysLeft: number | null
  /** "Valid", "Not valid", "No details held by DVLA" or "No results returned". */
  motStatus: string | null
  motExpiryDate: string | null
  motDaysLeft: number | null
  make: string | null
  colour: string | null
  fuelType: string | null
  /** Everything the DVLA sent, under its own field names. */
  record: Record<string, unknown>
  fetchedAt: string | null
  /** Why the attempt after fetchedAt failed, in English, for the console. */
  lastError: string | null
}

export interface DvlaStatus {
  licensePlate: string | null
  /** Three years after the registration date entered for the warranty; null without one. */
  firstMotDue: string | null
  info: DvlaInfo | null
}

export const dvlaApi = {
  get: (vin: string) => request<DvlaStatus>(`/api/vehicles/${vin}/dvla`),
}
