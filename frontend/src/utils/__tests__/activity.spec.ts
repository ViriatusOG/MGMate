import { describe, expect, it } from 'vitest'
import { activityRows, onlineShare } from '@/utils/activity'

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
})
