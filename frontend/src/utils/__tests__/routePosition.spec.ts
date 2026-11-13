import { describe, expect, it } from 'vitest'
import { bearing, positionAt } from '../routePosition'

const fix = (time: string, latitude: number, longitude: number) => ({
  recordedAt: `2026-11-13T11:${time}Z`,
  latitude,
  longitude,
})
const at = (time: string) => Date.parse(`2026-11-13T11:${time}Z`)

// North one step, then east two.
const route = [fix('00:00', 51.0, -1.0), fix('01:00', 51.01, -1.0), fix('03:00', 51.01, -0.98)]

describe('the car on the route at a time', () => {
  it('is that share of the way between the fixes either side', () => {
    const p = positionAt(route, at('00:30'))!
    expect(p.at[0]).toBeCloseTo(51.005)
    expect(p.at[1]).toBeCloseTo(-1.0)
    expect(p.heading).toBeCloseTo(0)
    expect(p.ahead).toEqual([p.at, [51.01, -1.0], [51.01, -0.98]])

    const q = positionAt(route, at('02:00'))!
    expect(q.at[1]).toBeCloseTo(-0.99)
    expect(q.heading).toBeCloseTo(90, 0)
    expect(q.ahead).toEqual([q.at, [51.01, -0.98]])
  })

  it('stays at the ends outside the drive', () => {
    expect(positionAt(route, at('00:00') - 60_000)!.at).toEqual([51.0, -1.0])
    const end = positionAt(route, at('09:00'))!
    expect(end.at).toEqual([51.01, -0.98])
    expect(end.heading).toBeCloseTo(90, 0)
    expect(end.ahead).toEqual([end.at])
  })

  it('heads the way the car moved off when it stood still at first', () => {
    const p = positionAt(
      [fix('00:00', 51, -1), fix('00:30', 51, -1), fix('01:00', 51.01, -1)],
      at('00:10'),
    )!
    expect(p.heading).toBeCloseTo(0)
  })

  it('has nothing to show without fixes', () => {
    expect(positionAt([], at('00:00'))).toBeNull()
  })

  it('reads bearings clockwise from north', () => {
    expect(bearing([51, -1], [50.99, -1])).toBeCloseTo(180)
    expect(bearing([51, -1], [51, -1.01])).toBeCloseTo(270, 0)
  })
})
