import { describe, expect, it } from 'vitest'
import { buildGpx, drivingPowerSign, summarizeTripTelemetry } from '@/utils/tripTelemetry'
import type { TripTelemetryPoint } from '@/services/tripLogApi'

const T0 = Date.parse('2026-11-07T08:00:00Z')

function at(minute: number, fields: Partial<TripTelemetryPoint>): TripTelemetryPoint {
  return {
    recordedAt: new Date(T0 + minute * 60_000).toISOString(),
    speed: null,
    socPercent: null,
    powerKw: null,
    elevation: null,
    outsideTemperature: null,
    climateOn: null,
    ...fields,
  }
}

describe('summarizeTripTelemetry', () => {
  it('splits battery power into what driving used and what braking put back', () => {
    // 20 kW for an hour, then -10 kW for half an hour, in one-minute readings.
    const points = [
      ...Array.from({ length: 61 }, (_, i) => at(i, { powerKw: 20 })),
      ...Array.from({ length: 30 }, (_, i) => at(61 + i, { powerKw: -10 })),
    ]
    const summary = summarizeTripTelemetry(points)
    expect(summary.usedKwh).toBeCloseTo(20 + 0.5 * (20 / 60), 1)
    expect(summary.regenKwh).toBeCloseTo(5, 0)
  })

  it('reads the same whichever sign the car gives discharging', () => {
    const points = Array.from({ length: 31 }, (_, i) => at(i, { powerKw: i % 10 === 0 ? 5 : -12 }))
    const summary = summarizeTripTelemetry(points)
    expect(summary.usedKwh!).toBeGreaterThan(summary.regenKwh!)
    expect(drivingPowerSign(points)).toBe(-1)
  })

  it('counts climb past GPS noise, outside temperature and climate time', () => {
    const elevations = [100, 101, 99, 100, 110, 120, 118, 119, 105, 100]
    const points = elevations.map((e, i) =>
      at(i, { elevation: e, outsideTemperature: 6 + i * 0.3, climateOn: i < 5 }),
    )
    const summary = summarizeTripTelemetry(points)
    expect(summary.climbM).toBe(20)
    expect(summary.descentM).toBe(20)
    expect(summary.outsideStart).toBe(6)
    expect(summary.outsideEnd).toBeCloseTo(8.7, 6)
    expect(summary.climateMinutes).toBe(5)
  })

  it('knows nothing from a trip with no readings', () => {
    expect(summarizeTripTelemetry([])).toEqual({
      usedKwh: null,
      regenKwh: null,
      climbM: null,
      descentM: null,
      outsideStart: null,
      outsideEnd: null,
      climateMinutes: null,
    })
  })
})

describe('buildGpx', () => {
  it('writes a track with times and the nearest elevation', () => {
    const gpx = buildGpx(
      'Home <> Work',
      [
        { recordedAt: new Date(T0).toISOString(), latitude: 51.75, longitude: -1.25, speed: 0 },
        {
          recordedAt: new Date(T0 + 60_000).toISOString(),
          latitude: 51.76,
          longitude: -1.24,
          speed: 40,
        },
      ],
      [at(0, { elevation: 61.04 })],
    )
    expect(gpx).toContain('<name>Home &#60;&#62; Work</name>')
    expect(gpx).toContain(
      '<trkpt lat="51.750000" lon="-1.250000"><ele>61.0</ele><time>2026-11-07T08:00:00.000Z</time></trkpt>',
    )
    expect(gpx.match(/<trkpt/g)).toHaveLength(2)
  })
})
