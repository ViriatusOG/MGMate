import { describe, expect, it } from 'vitest'
import type { ChargeSession } from '@/services/chargesApi'
import {
  billedKwh,
  chargeEnergy,
  chargeLoss,
  chargeMinutes,
  chargeTotals,
  monthStart,
} from '@/utils/charges'

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
    place: null,
    paidAmount: null,
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
        charge({ chargerEnergyKwh: 29 }),
        charge({ id: 2, chargeType: 'DC', energyAddedKwh: 30 }),
        charge({ id: 3, isHidden: true }),
      ],
      64,
    )
    expect(totals).toEqual({
      sessions: 2,
      kwh: 56,
      supplyKwh: 29,
      supplyBatteryKwh: 26,
      minutes: 480,
      ac: 1,
      dc: 1,
    })
  })

  // A meter bills what the charger drew from the supply, which is more than reached the battery.
  it('bills a charge on the supply figure and reads its loss', () => {
    const c = charge({ energyAddedKwh: 27, chargerEnergyKwh: 30 })
    expect(billedKwh(c, 64)).toBe(30)
    expect(chargeLoss(c)).toEqual({ kwh: 3, percent: 10 })
    expect(billedKwh(charge({ energyAddedKwh: 27 }), 64)).toBe(27)
    expect(chargeLoss(charge({ energyAddedKwh: 27 }))).toBeNull()
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
