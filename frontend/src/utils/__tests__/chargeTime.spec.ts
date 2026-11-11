import { describe, expect, it } from 'vitest'
import { chargeSecondsLeft, formatChargeTimeLeft } from '../chargeTime'

const t = (key: string, p?: Record<string, unknown>) =>
  key === 'trips.durationMinutes' ? `${p?.n} min` : `${p?.h}h ${p?.m}m`

describe('charge time left', () => {
  it('reads the gateway value as seconds, the way the MG app shows it', () => {
    // 22% to 100% at 6.4 kW: MG app "5 Hours 48 min"; the gateway sent 21060, not 351 hours.
    expect(formatChargeTimeLeft(21060, t)).toBe('5h 51m')
    expect(formatChargeTimeLeft(2700, t)).toBe('45 min')
  })

  it('has nothing to show without an estimate', () => {
    expect(chargeSecondsLeft(null)).toBeNull()
    expect(chargeSecondsLeft(0)).toBeNull()
    expect(chargeSecondsLeft(-1)).toBeNull()
    expect(formatChargeTimeLeft(undefined, t)).toBeNull()
  })
})
