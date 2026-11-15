import { describe, it, expect, beforeEach } from 'vitest'
import { chargeCurrentText, nextDailyStart } from '../carSchedule'
import { setRegionalFormat } from '@/utils/format'

const t = (key: string, named?: Record<string, unknown>) =>
  named ? `${key}:${JSON.stringify(named)}` : key

beforeEach(() => setRegionalFormat({ region: null, hourCycle: 'h23' }))

describe('nextDailyStart', () => {
  const evening = new Date(2026, 10, 15, 18, 0)

  it('is today while the time is still to come', () => {
    expect(nextDailyStart('23:30', evening, t)).toBe('climateSchedules.when.today:{"time":"23:30"}')
  })

  it('is tomorrow once the time has passed, seconds and all', () => {
    expect(nextDailyStart('06:45:00', evening, t)).toBe(
      'climateSchedules.when.tomorrow:{"time":"06:45"}',
    )
  })

  it('is tomorrow at the very minute it starts', () => {
    expect(nextDailyStart('18:00', evening, t)).toBe(
      'climateSchedules.when.tomorrow:{"time":"18:00"}',
    )
  })

  it('has nothing for a time it cannot read', () => {
    expect(nextDailyStart(null, evening, t)).toBeNull()
    expect(nextDailyStart('soon', evening, t)).toBeNull()
  })
})

describe('chargeCurrentText', () => {
  it('names the highest current Max, in any casing', () => {
    expect(chargeCurrentText('Max', t)).toBe('control.chargeLimitMax')
    expect(chargeCurrentText('MAX', t)).toBe('control.chargeLimitMax')
  })

  it('spaces the amps', () => {
    expect(chargeCurrentText('16A', t)).toBe('16 A')
  })

  it('has nothing when the car has not said', () => {
    expect(chargeCurrentText(null, t)).toBeNull()
  })
})
