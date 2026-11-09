import { describe, it, expect } from 'vitest'
import { monthsAndDays, sortCovers } from '@/utils/warranty'
import type { WarrantyCover } from '@/services/warrantyApi'

function cover(over: Partial<WarrantyCover>): WarrantyCover {
  return {
    id: 1,
    name: 'Cover',
    notes: null,
    months: 12,
    distanceKm: null,
    featured: false,
    sortOrder: 0,
    status: 'active',
    endsOn: '2027-07-31T00:00:00Z',
    daysRemaining: 264,
    kmRemaining: null,
    timeUsed: 0.3,
    distanceUsed: null,
    endedBy: null,
    ...over,
  }
}

describe('sortCovers', () => {
  it('lists what ends first, then expired, then what is not covered', () => {
    const sorted = sortCovers([
      cover({ id: 1, name: 'Tyres', status: 'notCovered', endsOn: null }),
      cover({ id: 2, name: 'Glass', status: 'expired', endsOn: '2026-10-31T00:00:00Z' }),
      cover({ id: 3, name: 'Paint', endsOn: '2029-07-31T00:00:00Z' }),
      cover({ id: 4, name: 'Wipers', status: 'endingSoon', endsOn: '2027-01-31T00:00:00Z' }),
      cover({ id: 5, name: 'Brakes', endsOn: '2027-07-31T00:00:00Z' }),
    ])

    expect(sorted.map((c) => c.name)).toEqual(['Wipers', 'Brakes', 'Paint', 'Glass', 'Tyres'])
  })
})

describe('monthsAndDays', () => {
  it('counts whole calendar months', () => {
    const from = new Date(Date.UTC(2026, 10, 9))
    expect(monthsAndDays(from, new Date(Date.UTC(2033, 6, 31)))).toEqual({ months: 80, days: 22 })
  })

  it('clamps a month end the way the server does', () => {
    const from = new Date(Date.UTC(2027, 0, 31))
    expect(monthsAndDays(from, new Date(Date.UTC(2027, 1, 28)))).toEqual({ months: 1, days: 0 })
  })
})
