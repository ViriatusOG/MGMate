import { describe, expect, it } from 'vitest'
import { capacityReadings, estimateCapacity } from '@/utils/batteryHealth'
import type { ChargeSession } from '@/services/chargesApi'

function charge(day: number, start: number, end: number, kwh: number | null): ChargeSession {
  return {
    id: day,
    startedAt: new Date(Date.UTC(2026, 0, 1) + day * 86_400_000).toISOString(),
    endedAt: new Date(Date.UTC(2026, 0, 1) + day * 86_400_000 + 3_600_000).toISOString(),
    socStartPercent: start,
    socEndPercent: end,
    energyAddedKwh: kwh,
    maxPowerKw: 7,
    chargeType: 'AC',
    latitude: null,
    longitude: null,
    odometerKm: null,
    stopReason: null,
    socTargetPercent: null,
    notes: null,
    place: null,
    paidAmount: null,
    isHidden: false,
  }
}

describe('battery health', () => {
  it('reads capacity off charges that cover enough of the battery and were measured', () => {
    const readings = capacityReadings(
      [
        charge(0, 30, 80, 32), // 64 kWh
        charge(1, 70, 80, 6.4), // too small a span
        charge(2, 20, 80, null), // not measured
        charge(3, 10, 60, 10), // 20 kWh: implausible against 64
      ],
      64,
    )
    expect(readings).toEqual([{ at: charge(0, 0, 0, 0).startedAt, kwh: 64, socSpan: 50 }])
  })

  it('weights the estimate by span and finds a falling trend', () => {
    const readings = [0, 60, 120, 180, 240].map((day, i) => ({
      at: charge(day, 0, 0, 0).startedAt,
      kwh: 64 - i * 0.5,
      socSpan: 50,
    }))
    const estimate = estimateCapacity(readings)!
    expect(estimate.kwh).toBeCloseTo(63, 6)
    expect(estimate.count).toBe(5)
    expect(estimate.trendPerYear!).toBeLessThan(0)
    expect(estimate.trend[0]!).toBeGreaterThan(estimate.trend[4]!)
  })

  it('has no estimate without readings and no trend from one', () => {
    expect(estimateCapacity([])).toBeNull()
    const one = estimateCapacity([{ at: charge(0, 0, 0, 0).startedAt, kwh: 64, socSpan: 40 }])!
    expect(one.trendPerYear).toBeNull()
    expect(one.spread).toBe(0)
  })
})
