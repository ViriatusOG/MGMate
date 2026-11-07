import type { ChargeSession } from '@/services/chargesApi'
import type { TripLogEntry } from '@/services/tripLogApi'
import { dayKey } from '@/utils/dayGroups'
import { chargeMinutes, monthStart } from '@/utils/charges'
import { loggedDistanceKm } from '@/utils/tripLog'

/** One month of driving and charging, as the Reports page adds it up. */
export interface MonthReport {
  key: string
  start: Date
  trips: number
  km: number
  drivingMs: number
  daysDriven: number
  /** The energy the trips used, over the distance of the trips it is known for. */
  kwhUsed: number
  kwhUsedKm: number
  charges: number
  kwhCharged: number
  chargingMinutes: number
  /** What the priced charges cost, and how many charges had no price. */
  chargingCost: number
  unpricedCharges: number
}

/** How the page knows what a trip used, what a charge added and what it cost. */
export interface ReportFigures {
  tripKwh: (entry: TripLogEntry) => number | null
  chargeKwh: (charge: ChargeSession) => number | null
  chargeCost: (charge: ChargeSession) => number | null
}

function monthKey(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
}

function emptyMonth(start: Date): MonthReport {
  return {
    key: monthKey(start),
    start,
    trips: 0,
    km: 0,
    drivingMs: 0,
    daysDriven: 0,
    kwhUsed: 0,
    kwhUsedKm: 0,
    charges: 0,
    kwhCharged: 0,
    chargingMinutes: 0,
    chargingCost: 0,
    unpricedCharges: 0,
  }
}

/**
 * The `count` months up to and including `last`, oldest first. Hidden trips and charges stay out,
 * as they stay out of every other total.
 */
export function monthReports(
  last: Date,
  count: number,
  trips: readonly TripLogEntry[],
  charges: readonly ChargeSession[],
  figures: ReportFigures,
): MonthReport[] {
  const months = Array.from({ length: count }, (_, i) =>
    emptyMonth(monthStart(last, i - count + 1)),
  )
  const byKey = new Map(months.map((m) => [m.key, m]))
  const days = new Map<string, Set<string>>()

  for (const trip of trips) {
    if (trip.isHidden) continue
    const started = new Date(trip.startedAt)
    const month = byKey.get(monthKey(started))
    if (!month) continue
    const km = loggedDistanceKm(trip) ?? 0
    month.trips++
    month.km += km
    month.drivingMs += Math.max(0, Date.parse(trip.endedAt) - started.getTime())
    const kwh = figures.tripKwh(trip)
    if (kwh !== null) {
      month.kwhUsed += kwh
      month.kwhUsedKm += km
    }
    let driven = days.get(month.key)
    if (!driven) days.set(month.key, (driven = new Set()))
    driven.add(dayKey(started))
  }

  for (const charge of charges) {
    if (charge.isHidden) continue
    const month = byKey.get(monthKey(new Date(charge.startedAt)))
    if (!month) continue
    month.charges++
    month.kwhCharged += figures.chargeKwh(charge) ?? 0
    month.chargingMinutes += chargeMinutes(charge)
    const cost = figures.chargeCost(charge)
    if (cost === null) month.unpricedCharges++
    else month.chargingCost += cost
  }

  for (const month of months) month.daysDriven = days.get(month.key)?.size ?? 0
  return months
}

/** Wh per km over the trips whose energy is known, or null without any. */
export function reportEfficiency(month: MonthReport): number | null {
  return month.kwhUsed > 0 && month.kwhUsedKm > 0 ? (month.kwhUsed * 1000) / month.kwhUsedKm : null
}

/** True when nothing was driven or charged in the month. */
export function isEmptyMonth(month: MonthReport): boolean {
  return month.trips === 0 && month.charges === 0
}

/**
 * How much a figure moved since the month before, as a fraction. Null when either side has
 * nothing to compare: an empty month would read as −100 %, which says more about the calendar
 * than about the driving.
 */
export function monthChange(current: number | null, previous: number | null): number | null {
  if (current === null || previous === null || previous === 0) return null
  return (current - previous) / previous
}
