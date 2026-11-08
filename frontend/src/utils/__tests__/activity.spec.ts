import { describe, expect, it } from 'vitest'
import {
  activityRows,
  activitySpans,
  activityTone,
  onlineShare,
  spansTotalMs,
  timesWoken,
} from '@/utils/activity'

describe('activity', () => {
  it('lists events and gaps together, newest first', () => {
    const rows = activityRows(
      [
        { at: '2026-11-07T08:00:00Z', kind: 'unlocked', socPercent: 60 },
        { at: '2026-11-07T12:00:00Z', kind: 'locked', socPercent: 55 },
      ],
      [{ from: '2026-11-07T09:00:00Z', to: '2026-11-07T11:00:00Z' }],
    )
    expect(rows.map((r) => r.type)).toEqual(['event', 'gap', 'event'])
  })

  it('works out how much of a period the car reported', () => {
    const from = new Date('2026-11-06T00:00:00Z')
    const to = new Date('2026-11-07T00:00:00Z')
    expect(
      onlineShare([{ from: '2026-11-06T00:00:00Z', to: '2026-11-06T06:00:00Z' }], from, to),
    ).toBe(0.75)
    expect(onlineShare([], from, to)).toBe(1)
  })

  it('pairs start and end events into spans, clipped to the day', () => {
    const from = new Date('2026-11-06T00:00:00Z')
    const to = new Date('2026-11-07T00:00:00Z')
    const spans = activitySpans(
      [
        { at: '2026-11-06T01:00:00Z', kind: 'chargingStopped', socPercent: 80 },
        { at: '2026-11-06T08:00:00Z', kind: 'driveStarted', socPercent: 80 },
        { at: '2026-11-06T08:30:00Z', kind: 'driveEnded', socPercent: 75 },
        { at: '2026-11-06T22:00:00Z', kind: 'chargingStarted', socPercent: 60 },
      ],
      'chargingStarted',
      'chargingStopped',
      from,
      to,
    )
    expect(spans).toEqual([
      { from: from.getTime(), to: Date.parse('2026-11-06T01:00:00Z') },
      { from: Date.parse('2026-11-06T22:00:00Z'), to: to.getTime() },
    ])
    expect(spansTotalMs(spans)).toBe(3 * 3600_000)
  })

  it('counts the times the car came back from a quiet stretch', () => {
    const from = new Date('2026-11-06T00:00:00Z')
    const to = new Date('2026-11-07T00:00:00Z')
    const gaps = [
      { from: '2026-11-05T22:00:00Z', to: '2026-11-06T06:00:00Z' },
      { from: '2026-11-06T09:00:00Z', to: '2026-11-06T12:00:00Z' },
      { from: '2026-11-06T23:00:00Z', to: '2026-11-07T05:00:00Z' },
    ]
    expect(timesWoken(gaps, from, to)).toBe(2)
  })

  it('groups event kinds by what they are about', () => {
    expect(activityTone('driveEnded')).toBe('drive')
    expect(activityTone('plugged')).toBe('charge')
    expect(activityTone('climateOn')).toBe('climate')
    expect(activityTone('doorOpened')).toBe('access')
  })
})
