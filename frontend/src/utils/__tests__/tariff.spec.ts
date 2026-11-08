import { describe, expect, it } from 'vitest'
import {
  chargeCost,
  chargePlace,
  homeCost,
  homePriceAt,
  parseTariff,
  type Tariff,
} from '@/utils/tariff'
import type { CommutePlace } from '@/services/commutePlacesApi'

const timeOfUse: Tariff = {
  currency: 'GBP',
  home: [
    { from: '00:30', to: '05:30', price: 0.07, days: 'all' },
    { from: '05:30', to: '00:30', price: 0.245, days: 'all' },
  ],
  publicPrice: 0.79,
}

const home: CommutePlace = {
  id: 1,
  kind: 'home',
  label: null,
  latitude: 51.75,
  longitude: -1.25,
  radiusMeters: 250,
  notify: false,
}

// Local times, so the tests hold in any time zone the runner is in.
const local = (h: number, m = 0) => new Date(2026, 10, 19, h, m).toISOString()

const charge = {
  paidAmount: null,
  place: null,
  latitude: 51.75,
  longitude: -1.25,
  startedAt: local(1),
  endedAt: local(5),
}

describe('tariff', () => {
  it('finds the band covering a moment, including one past midnight', () => {
    expect(homePriceAt(timeOfUse, new Date(2026, 10, 19, 2))).toBe(0.07)
    expect(homePriceAt(timeOfUse, new Date(2026, 10, 19, 23))).toBe(0.245)
    expect(homePriceAt(timeOfUse, new Date(2026, 10, 19, 0, 15))).toBe(0.245)
  })

  it('splits a charge across the bands it spans', () => {
    // Four hours from 03:30: two off-peak, two peak, 10 kWh spread evenly.
    const cost = homeCost(timeOfUse, local(3, 30), local(7, 30), 10)
    expect(cost).toBeCloseTo(5 * 0.07 + 5 * 0.245, 6)
  })

  it('prices a charge at the home place from the home bands', () => {
    expect(chargePlace(charge, [home])).toBe('home')
    expect(chargeCost(charge, 20, timeOfUse, [home])).toEqual({
      amount: expect.closeTo(1.4, 6),
      source: 'home',
    })
  })

  it('prices anywhere else at the public price, and a typed-in price wins', () => {
    const away = { ...charge, latitude: 52.0, longitude: -1.0 }
    expect(chargeCost(away, 10, timeOfUse, [home])).toEqual({
      amount: expect.closeTo(7.9, 6),
      source: 'public',
    })
    expect(chargeCost({ ...away, paidAmount: 6.5 }, 10, timeOfUse, [home])).toEqual({
      amount: 6.5,
      source: 'paid',
    })
    expect(chargeCost({ ...charge, place: 'work' }, 10, timeOfUse, [home])?.source).toBe('work')
  })

  it('reads back only a usable tariff', () => {
    expect(parseTariff(null)).toBeNull()
    expect(parseTariff({ currency: 'XYZ', home: [] })).toBeNull()
    expect(
      parseTariff({
        currency: 'EUR',
        home: [{ from: '25:00', to: '01:00', price: 1, days: 'all' }, timeOfUse.home[0]],
        publicPrice: -1,
      }),
    ).toEqual({ currency: 'EUR', home: [timeOfUse.home[0]], publicPrice: null })
  })
})
