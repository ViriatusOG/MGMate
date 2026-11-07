import { describe, expect, it } from 'vitest'
import type { ChargeSession } from '@/services/chargesApi'
import type { TripLogEntry } from '@/services/tripLogApi'
import { isEmptyMonth, monthChange, monthReports, reportEfficiency } from '@/utils/reports'

function trip(startedAt: string, km: number, extra: Partial<TripLogEntry> = {}): TripLogEntry {
  const start = new Date(startedAt)
  return {
    id: start.getTime(),
    startedAt: start.toISOString(),
    endedAt: new Date(start.getTime() + 30 * 60_000).toISOString(),
    distanceKm: km,
    odometerStartKm: null,
    odometerEndKm: null,
    isHidden: false,
    ...extra,
  } as TripLogEntry
}

function charge(startedAt: string, extra: Partial<ChargeSession> = {}): ChargeSession {
  const start = new Date(startedAt)
  return {
    id: start.getTime(),
    startedAt: start.toISOString(),
    endedAt: new Date(start.getTime() + 60 * 60_000).toISOString(),
    isHidden: false,
    ...extra,
  } as ChargeSession
}

const figures = {
  tripKwh: (t: TripLogEntry) => t.distanceKm * 0.15,
  chargeKwh: () => 20,
  chargeCost: (c: ChargeSession) => c.paidAmount,
}

describe('monthReports', () => {
  it('adds up each month, oldest first, without hidden entries', () => {
    const months = monthReports(
      new Date(2026, 10, 1),
      3,
      [
        trip('2026-10-03T08:00', 20),
        trip('2026-10-03T17:00', 20),
        trip('2026-10-09T08:00', 10),
        trip('2026-11-02T08:00', 40),
        trip('2026-11-04T08:00', 99, { isHidden: true }),
        trip('2026-06-01T08:00', 5),
      ],
      [
        charge('2026-10-05T21:00', { paidAmount: 6 }),
        charge('2026-11-01T21:00', { paidAmount: null }),
      ],
      figures,
    )

    expect(months.map((m) => m.key)).toEqual(['2026-09', '2026-10', '2026-11'])
    const [sep, oct, nov] = months
    expect(isEmptyMonth(sep!)).toBe(true)
    expect(oct).toMatchObject({ trips: 3, km: 50, daysDriven: 2, drivingMs: 90 * 60_000 })
    expect(oct).toMatchObject({ charges: 1, kwhCharged: 20, chargingMinutes: 60, chargingCost: 6 })
    expect(reportEfficiency(oct!)).toBeCloseTo(150, 6)
    expect(nov).toMatchObject({ trips: 1, km: 40, charges: 1, unpricedCharges: 1, chargingCost: 0 })
  })
})

describe('monthChange', () => {
  it('compares only when both months have something', () => {
    expect(monthChange(120, 100)).toBeCloseTo(0.2, 6)
    expect(monthChange(0, 100)).toBe(-1)
    expect(monthChange(50, 0)).toBeNull()
    expect(monthChange(null, 100)).toBeNull()
  })
})
