import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import ReportsView from '../ReportsView.vue'
import { chargesApi, type ChargeSession } from '@/services/chargesApi'
import { tripLogApi, type TripLogEntry } from '@/services/tripLogApi'
import { commutePlacesApi } from '@/services/commutePlacesApi'
import { vehicleApi, type Vehicle } from '@/services/vehicleApi'

vi.mock('@/services/chargesApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/chargesApi')>()
  return { ...actual, chargesApi: { log: vi.fn<typeof actual.chargesApi.log>() } }
})

vi.mock('@/services/tripLogApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/tripLogApi')>()
  return { ...actual, tripLogApi: { log: vi.fn<typeof actual.tripLogApi.log>() } }
})

vi.mock('@/services/commutePlacesApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/commutePlacesApi')>()
  return {
    ...actual,
    commutePlacesApi: {
      ...actual.commutePlacesApi,
      list: vi.fn<typeof actual.commutePlacesApi.list>(),
    },
  }
})

vi.mock('@/services/vehicleApi', () => ({
  vehicleApi: { list: vi.fn<() => Promise<Vehicle[]>>() },
}))

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })
const car = { id: 1, vin: 'FAKEVN00000000001', vehicleType: 'bev' } as Vehicle

function trip(startedAt: string, km: number): TripLogEntry {
  const start = new Date(startedAt)
  return {
    id: start.getTime(),
    startedAt: start.toISOString(),
    endedAt: new Date(start.getTime() + 30 * 60_000).toISOString(),
    distanceKm: km,
    odometerStartKm: null,
    odometerEndKm: null,
    isHidden: false,
  } as TripLogEntry
}

let pinia: Pinia

async function mountView() {
  const wrapper = mount(ReportsView, {
    global: { plugins: [i18n, pinia], stubs: { FontAwesomeIcon: true } },
  })
  await flushPromises()
  return wrapper
}

describe('ReportsView', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    vi.clearAllMocks()
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 10, 7, 12))
    vi.mocked(vehicleApi.list).mockResolvedValue([car])
    vi.mocked(commutePlacesApi.list).mockResolvedValue([])
    vi.mocked(tripLogApi.log).mockResolvedValue([
      trip('2026-10-03T08:00', 100),
      trip('2026-11-02T08:00', 120),
    ])
    vi.mocked(chargesApi.log).mockResolvedValue([] as ChargeSession[])
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('opens on this month, set against the month before, over the year up to it', async () => {
    const wrapper = await mountView()

    expect(tripLogApi.log).toHaveBeenCalledWith(
      car.vin,
      new Date(2025, 11, 1).toISOString(),
      new Date(2026, 11, 1).toISOString(),
    )
    const distance = wrapper.findAll('.reports__tile')[0]!.text()
    expect(distance).toContain('120')
    expect(distance).toContain('▲ 20% vs Oct')
    // Dec to Sep had no trips, so they fold into one gap row before Oct and Nov.
    expect(wrapper.findAll('.reports__table tbody tr')).toHaveLength(3)
    expect(wrapper.findAll('.reports__bar')).toHaveLength(12)
    expect(wrapper.find('.reports__row--selected').text()).toContain('Nov 2026')
  })

  it('shows another month when its row is picked', async () => {
    const wrapper = await mountView()

    await wrapper.findAll('.reports__row-button')[1]!.trigger('click')

    expect(wrapper.find('.reports__month-title').text()).toBe('October 2026')
    expect(wrapper.find('.reports__row--selected').text()).toContain('Oct 2026')
    // September had nothing to compare with.
    expect(wrapper.findAll('.reports__tile')[0]!.text()).not.toContain('vs')
  })
})
