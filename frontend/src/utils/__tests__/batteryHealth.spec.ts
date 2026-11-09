import { describe, expect, it } from 'vitest'
import { capacityReadings, estimateCapacity, healthPercent } from '@/utils/batteryHealth'
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
    expect(readings).toEqual([
      { at: charge(0, 0, 0, 0).startedAt, kwh: 64, socSpan: 50, calibrated: false },
    ])
  })

  it('weights the estimate by span and finds a falling trend', () => {
    const readings = [0, 60, 120, 180, 240].map((day, i) => ({
      at: charge(day, 0, 0, 0).startedAt,
      kwh: 64 - i * 0.5,
      socSpan: 50,
      calibrated: true,
    }))
    const estimate = estimateCapacity(readings)!
    expect(estimate.kwh).toBeCloseTo(63, 6)
    expect(estimate.count).toBe(5)
    expect(estimate.trendPerYear!).toBeLessThan(0)
    expect(estimate.trend[0]!).toBeGreaterThan(estimate.trend[4]!)
  })

  it('has no estimate without readings and no trend from one', () => {
    expect(estimateCapacity([])).toBeNull()
    const one = estimateCapacity([
      { at: charge(0, 0, 0, 0).startedAt, kwh: 64, socSpan: 40, calibrated: true },
    ])!
    expect(one.trendPerYear).toBeNull()
    expect(one.spread).toBe(0)
  })

  it('trusts a reading only within a month after a full charge', () => {
    const readings = capacityReadings(
      [
        charge(0, 40, 80, 26.8), // before any full charge: 67 kWh, drifted
        charge(1, 30, 100, 44), // the full charge itself started uncalibrated
        charge(5, 40, 80, 25.2), // 4 days after the full charge: 63 kWh
        charge(60, 40, 80, 26.8), // two months on: drifted again
      ],
      64,
    )
    expect(readings.map((r) => r.calibrated)).toEqual([false, false, true, false])
    const estimate = estimateCapacity(readings)!
    expect(estimate.calibrated).toBe(true)
    expect(estimate.count).toBe(1)
    expect(estimate.kwh).toBeCloseTo(63, 6)
  })

  it('shows no health until calibrated, and never over 100%', () => {
    const raw = capacityReadings([charge(0, 40, 80, 26.8), charge(2, 30, 70, 26.8)], 64)
    const first = estimateCapacity(raw)!
    expect(first.calibrated).toBe(false)
    expect(first.kwh).toBeCloseTo(67, 6)
    expect(healthPercent(first, 64)).toBeNull()

    const above = estimateCapacity([
      { at: charge(0, 0, 0, 0).startedAt, kwh: 67, socSpan: 40, calibrated: true },
    ])
    expect(healthPercent(above, 64)).toBe(100)
    const below = estimateCapacity([
      { at: charge(0, 0, 0, 0).startedAt, kwh: 60.8, socSpan: 40, calibrated: true },
    ])
    expect(healthPercent(below, 64)).toBeCloseTo(95, 6)
    expect(healthPercent(below, null)).toBeNull()
  })
})
