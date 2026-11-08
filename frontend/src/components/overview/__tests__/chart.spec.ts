import { describe, expect, it } from 'vitest'
import { niceTop, timeTicks } from '../chart'

describe('niceTop', () => {
  it('rounds a DC peak to a three-tick scale', () => {
    expect(niceTop(96.9)).toBe(100)
  })

  it('keeps a home AC charge on a small scale', () => {
    expect(niceTop(7.2)).toBe(8)
    expect(niceTop(11)).toBe(12)
  })

  it('never returns zero', () => {
    expect(niceTop(0)).toBe(1)
  })
})

describe('timeTicks', () => {
  const MIN = 60_000

  it('always ends on the last time', () => {
    const from = Date.UTC(2026, 10, 7, 13, 41)
    const ticks = timeTicks(from, from + 16 * MIN, 300)
    expect(ticks[ticks.length - 1]).toBe(from + 16 * MIN)
    expect(ticks[0]).toBe(from)
  })

  it('leaves out ticks that would crowd each other', () => {
    const from = Date.UTC(2026, 10, 7, 9, 34)
    const width = 120
    const ticks = timeTicks(from, from + 24 * 60 * MIN, width)
    const px = ticks.map((t) => ((t - from) / (24 * 60 * MIN)) * width)
    for (let i = 1; i < px.length; i++) expect(px[i]! - px[i - 1]!).toBeGreaterThanOrEqual(44)
  })

  it('puts a day on whole hours', () => {
    const from = new Date(2026, 10, 7, 9, 34).getTime()
    const ticks = timeTicks(from, from + 24 * 60 * MIN, 400)
    for (const t of ticks.slice(0, -1)) expect(new Date(t).getMinutes()).toBe(0)
  })
})
