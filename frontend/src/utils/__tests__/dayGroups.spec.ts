import { describe, expect, it } from 'vitest'
import { dayKey, groupByDay, monthCells } from '@/utils/dayGroups'

describe('dayGroups', () => {
  it('groups by local day, newest first both ways', () => {
    const at = (d: number, h: number) => new Date(2026, 9, d, h).toISOString()
    const groups = groupByDay([{ at: at(1, 8) }, { at: at(1, 17) }, { at: at(3, 9) }], (i) => i.at)
    expect(groups.map((g) => g.key)).toEqual(['2026-10-03', '2026-10-01'])
    expect(groups[1]!.items.map((i) => new Date(i.at).getHours())).toEqual([17, 8])
  })

  it('starts a month on the right weekday', () => {
    // 1 October 2026 is a Thursday: three blanks before it.
    const cells = monthCells(new Date(2026, 9, 1))
    expect(cells.slice(0, 4)).toEqual([null, null, null, 1])
    expect(cells[cells.length - 1]).toBe(31)
    expect(dayKey(new Date(2026, 0, 5))).toBe('2026-01-05')
  })
})
