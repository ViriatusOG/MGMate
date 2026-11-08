import { describe, it, expect } from 'vitest'
import { dueProgress, dueStatusVariant, formatIntervalSummary } from '@/utils/maintenance'
import { METRIC_UNITS, UnitFormatter } from '@/utils/units'

const t = (key: string, named?: Record<string, unknown>) =>
  named ? `${key}:${Object.values(named).join(',')}` : key
const units = new UnitFormatter(METRIC_UNITS, t)

describe('formatIntervalSummary', () => {
  it('joins distance and time intervals with the localized "or"', () => {
    expect(formatIntervalSummary({ intervalKm: 15000, intervalMonths: 12 }, t, units)).toBe(
      'maintenance.everyDistance:15,000 units.km maintenance.or maintenance.everyMonths:12',
    )
  })

  it('renders the distance in the unit the browser shows', () => {
    const miles = new UnitFormatter({ ...METRIC_UNITS, distance: 'mi' }, t)
    expect(formatIntervalSummary({ intervalKm: 15000, intervalMonths: null }, t, miles)).toBe(
      'maintenance.everyDistance:9,321 units.mi',
    )
  })

  it('renders only the interval that is set', () => {
    expect(formatIntervalSummary({ intervalKm: null, intervalMonths: 6 }, t, units)).toBe(
      'maintenance.everyMonths:6',
    )
  })

  it('is empty when neither interval is set', () => {
    expect(formatIntervalSummary({ intervalKm: null, intervalMonths: null }, t, units)).toBe('')
  })
})

describe('dueStatusVariant', () => {
  it('colours each due status, with an item never serviced as information', () => {
    expect(
      (['overdue', 'dueSoon', 'ok', 'unknown'] as const).map((status) => dueStatusVariant(status)),
    ).toEqual(['danger', 'warning', 'success', 'info'])
  })
})

describe('dueProgress', () => {
  it('measures how far through each interval an item is', () => {
    const p = dueProgress({
      intervalKm: 10000,
      intervalMonths: 12,
      kmRemaining: 2500,
      daysRemaining: -10,
    })
    expect(p.distance).toEqual({ used: 0.75, remaining: 2500 })
    expect(p.time).toEqual({ used: 1, remaining: -10 })
  })

  it('leaves out an interval the item has no figures for', () => {
    const p = dueProgress({
      intervalKm: null,
      intervalMonths: 24,
      kmRemaining: null,
      daysRemaining: null,
    })
    expect(p).toEqual({ distance: null, time: null })
  })
})
