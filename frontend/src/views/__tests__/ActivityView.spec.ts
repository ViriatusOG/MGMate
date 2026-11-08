import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import ActivityView from '../ActivityView.vue'
import { activityApi, type ActivityTimeline } from '@/services/activityApi'
import { vehicleApi, type Vehicle } from '@/services/vehicleApi'

vi.mock('@/services/activityApi', () => ({
  activityApi: { timeline: vi.fn<() => Promise<ActivityTimeline>>() },
}))

vi.mock('@/services/vehicleApi', () => ({
  vehicleApi: { list: vi.fn<() => Promise<Vehicle[]>>() },
}))

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })
const car = { id: 1, vin: 'FAKEVN00000000001', vehicleType: 'bev' } as Vehicle

function at(hour: number, minute = 0): string {
  return new Date(2026, 10, 7, hour, minute).toISOString()
}

describe('ActivityView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 10, 7, 23))
    vi.mocked(vehicleApi.list).mockResolvedValue([car])
    vi.mocked(activityApi.timeline).mockResolvedValue({
      events: [
        { at: at(8), kind: 'unlocked', socPercent: 80 },
        { at: at(8, 5), kind: 'driveStarted', socPercent: 80 },
        { at: at(9, 5), kind: 'driveEnded', socPercent: 70 },
        { at: at(18), kind: 'chargingStarted', socPercent: 60 },
        { at: at(20), kind: 'chargingStopped', socPercent: 90 },
      ],
      gaps: [{ from: at(1), to: at(6) }],
      hours: [],
      readings: 120,
    })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('sums the day into driving and charging time, and draws them on the strip', async () => {
    const wrapper = mount(ActivityView, {
      global: { plugins: [i18n], stubs: { FontAwesomeIcon: true, StatsChartCard: true } },
    })
    await flushPromises()

    const tiles = wrapper.findAll('.stat-tile').map((tile) => tile.text())
    expect(tiles[0]).toContain('1h 0m')
    expect(tiles[1]).toContain('2h 0m')
    expect(tiles[3]).toContain('1')
    expect(wrapper.findAll('.activity__seg--drive')).toHaveLength(1)
    expect(wrapper.findAll('.activity__seg--charge')).toHaveLength(1)
    expect(wrapper.findAll('.activity__seg--quiet')).toHaveLength(1)
    expect(wrapper.findAll('.activity__tick')).toHaveLength(1)
    expect(wrapper.findAll('.activity__timeline > li')).toHaveLength(6)
  })
})
