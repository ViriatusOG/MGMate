import { describe, expect, it } from 'vitest'
import type { ChargeSession } from '@/services/chargesApi'
import { chargeEnergy, chargeMinutes, chargeTotals, monthStart } from '@/utils/charges'

function charge(overrides: Partial<ChargeSession> = {}): ChargeSession {
  return {
    id: 1,
    startedAt: '2026-11-07T22:00:00Z',
    endedAt: '2026-11-08T02:00:00Z',
    socStartPercent: 40,
    socEndPercent: 80,
    energyAddedKwh: 26,
    maxPowerKw: 7.2,
    chargeType: 'AC',
    latitude: null,
    longitude: null,
    odometerKm: null,
    stopReason: null,
    socTargetPercent: 80,
    notes: null,
    isHidden: false,
    ...overrides,
  }
}

describe('chargeEnergy', () => {
  it('takes the measured figure first', () => {
    expect(chargeEnergy(charge(), 64)).toEqual({ kwh: 26, source: 'measured' })
  })

  it('estimates from the state of charge without one', () => {
    const energy = chargeEnergy(charge({ energyAddedKwh: null }), 64)
    expect(energy?.source).toBe('soc')
    expect(energy?.kwh).toBeCloseTo(25.6, 6)
  })

  it('gives nothing without a capacity or a rise', () => {
    expect(chargeEnergy(charge({ energyAddedKwh: null }), null)).toBeNull()
    expect(chargeEnergy(charge({ energyAddedKwh: null, socEndPercent: 40 }), 64)).toBeNull()
  })
})

describe('chargeTotals', () => {
  it('adds up the charges that are not hidden', () => {
    const totals = chargeTotals(
      [
        charge(),
        charge({ id: 2, chargeType: 'DC', energyAddedKwh: 30 }),
        charge({ id: 3, isHidden: true }),
      ],
      64,
    )
    expect(totals).toEqual({ sessions: 2, kwh: 56, minutes: 480, ac: 1, dc: 1 })
  })
})

describe('chargeMinutes and monthStart', () => {
  it('counts the minutes of a charge', () => {
    expect(chargeMinutes(charge())).toBe(240)
  })

  it('steps whole months', () => {
    expect(monthStart(new Date(2026, 10, 17), -1)).toEqual(new Date(2026, 9, 1))
    expect(monthStart(new Date(2026, 0, 5), -1)).toEqual(new Date(2025, 11, 1))
  })
})
