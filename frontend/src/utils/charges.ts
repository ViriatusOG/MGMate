import type { ChargeSession } from '@/services/chargesApi'

/** What a charge put into the battery, and where that figure came from. */
export interface ChargeEnergy {
  kwh: number
  /** Measured from the battery power, or estimated from the rise in state of charge. */
  source: 'measured' | 'soc'
}

/**
 * The energy a charge added: the measured figure when the car reported enough power, else the rise
 * in state of charge times the battery's capacity.
 */
export function chargeEnergy(
  charge: Pick<ChargeSession, 'energyAddedKwh' | 'socStartPercent' | 'socEndPercent'>,
  capacityKwh: number | null,
): ChargeEnergy | null {
  if (charge.energyAddedKwh !== null && charge.energyAddedKwh > 0) {
    return { kwh: charge.energyAddedKwh, source: 'measured' }
  }
  const { socStartPercent: start, socEndPercent: end } = charge
  if (start === null || end === null || capacityKwh === null || capacityKwh <= 0) return null
  const kwh = ((end - start) / 100) * capacityKwh
  return kwh > 0 ? { kwh, source: 'soc' } : null
}

/** Minutes from the first charging reading to the last; never negative. */
export function chargeMinutes(charge: Pick<ChargeSession, 'startedAt' | 'endedAt'>): number {
  return Math.max(0, (Date.parse(charge.endedAt) - Date.parse(charge.startedAt)) / 60_000)
}

export interface ChargeTotals {
  sessions: number
  kwh: number
  minutes: number
  ac: number
  dc: number
}

/** The month's figures over the charges that are not hidden. */
export function chargeTotals(
  charges: readonly ChargeSession[],
  capacityKwh: number | null,
): ChargeTotals {
  const totals: ChargeTotals = { sessions: 0, kwh: 0, minutes: 0, ac: 0, dc: 0 }
  for (const charge of charges) {
    if (charge.isHidden) continue
    totals.sessions++
    totals.kwh += chargeEnergy(charge, capacityKwh)?.kwh ?? 0
    totals.minutes += chargeMinutes(charge)
    if (charge.chargeType === 'DC') totals.dc++
    else totals.ac++
  }
  return totals
}

/** The first day of the month `offset` months from the one holding `date`, at local midnight. */
export function monthStart(date: Date, offset = 0): Date {
  return new Date(date.getFullYear(), date.getMonth() + offset, 1)
}
