import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import ChargesView from '../ChargesView.vue'
import { chargesApi, type ChargeSession } from '@/services/chargesApi'
import { commutePlacesApi } from '@/services/commutePlacesApi'
import { vehicleApi, type Vehicle } from '@/services/vehicleApi'

vi.mock('@/services/chargesApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/chargesApi')>()
  return {
    ...actual,
    chargesApi: {
      log: vi.fn<typeof actual.chargesApi.log>(),
      chart: vi.fn<typeof actual.chargesApi.chart>(),
      update: vi.fn<typeof actual.chargesApi.update>(),
      setHidden: vi.fn<typeof actual.chargesApi.setHidden>(),
      mergeWithNext: vi.fn<typeof actual.chargesApi.mergeWithNext>(),
    },
  }
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

function charge(overrides: Partial<ChargeSession>): ChargeSession {
  return {
    id: 1,
    startedAt: '2026-10-01T21:00:00Z',
    endedAt: '2026-10-02T01:00:00Z',
    socStartPercent: 30,
    socEndPercent: 80,
    energyAddedKwh: 30,
    maxPowerKw: 7.2,
    chargeType: 'AC',
    latitude: null,
    longitude: null,
    odometerKm: null,
    stopReason: null,
    socTargetPercent: null,
    notes: null,
    place: null,
    paidAmount: null,
    isHidden: false,
    ...overrides,
  }
}

// Oldest first, as the API answers.
const charges = [
  charge({ id: 1, place: 'home' }),
  charge({
    id: 2,
    startedAt: '2026-10-05T12:00:00Z',
    endedAt: '2026-10-05T12:30:00Z',
    chargeType: 'DC',
    maxPowerKw: 96,
    energyAddedKwh: 25,
    place: 'public',
    paidAmount: 17.5,
  }),
  charge({ id: 3, startedAt: '2026-10-08T21:00:00Z', isHidden: true }),
]

let pinia: Pinia

async function mountView() {
  const wrapper = mount(ChargesView, {
    global: {
      plugins: [i18n, pinia],
      stubs: { FontAwesomeIcon: true, StatsChartCard: true, TariffModal: true, teleport: true },
    },
  })
  await flushPromises()
  return wrapper
}

describe('ChargesView', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    vi.clearAllMocks()
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-19T12:00:00Z'))
    vi.mocked(vehicleApi.list).mockResolvedValue([car])
    vi.mocked(commutePlacesApi.list).mockResolvedValue([])
    vi.mocked(chargesApi.log).mockResolvedValue(charges)
    vi.mocked(chargesApi.chart).mockResolvedValue([
      { recordedAt: '2026-10-05T12:00:00Z', powerKw: -90, socPercent: 38, outsideTemperature: 9 },
      { recordedAt: '2026-10-05T12:15:00Z', powerKw: -60, socPercent: 60, outsideTemperature: 11 },
    ])
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('lists the charges by day, newest first, under the days that have them', async () => {
    const wrapper = await mountView()

    const days = wrapper.findAll('.ll-day').map((d) => d.attributes('id'))
    expect(days).toEqual([
      'charge-day-2026-10-08',
      'charge-day-2026-10-05',
      'charge-day-2026-10-01',
    ])
    expect(wrapper.findAll('.month-cal__day--active')).toHaveLength(2)
  })

  it('counts AC and DC, and home and public, without the hidden charge', async () => {
    const wrapper = await mountView()

    const tiles = wrapper.find('.stat-tiles').text()
    expect(tiles).toContain('55.0kWh')
    expect(tiles).toContain('96.0 kW')
    const rings = wrapper.findAll('.split-card').map((d) => d.text().replace(/\s+/g, ' '))
    expect(rings[0]).toContain('1 AC')
    expect(rings[0]).toContain('1 DC')
    expect(rings[1]).toContain('1 Home')
    expect(rings[1]).toContain('1 Public')
  })

  it('opens a charge with its chart and figures when picked', async () => {
    const wrapper = await mountView()

    await wrapper.findAll('.ll-row')[1]!.trigger('click')
    await flushPromises()

    expect(chargesApi.chart).toHaveBeenCalledWith(car.vin, 2)
    const detail = wrapper.find('.charges__detail').text()
    expect(detail).toContain('75.0 kW')
    expect(detail).toContain('Outside')
  })

  it('searches the charges over the days asked for and adds up what it found', async () => {
    const wrapper = await mountView()

    const search = wrapper.find('.log-search')
    await search.find('input[type=search]').setValue('dc')
    const [from, to] = search.findAll('input[type=date]')
    await from!.setValue('2026-01-01')
    await to!.setValue('2026-10-19')
    await search.trigger('submit')
    await flushPromises()

    expect(chargesApi.log).toHaveBeenLastCalledWith(
      car.vin,
      new Date(2026, 0, 1).toISOString(),
      new Date(2026, 9, 20).toISOString(),
    )
    const results = wrapper.find('.log-search-results').text()
    expect(results).toContain('1 charge found')
    expect(results).toContain('25.0 kWh')
    expect(wrapper.findAll('.ll-row')).toHaveLength(1)

    await wrapper.find('.log-search-results button').trigger('click')
    await flushPromises()
    expect(wrapper.find('.log-search-results').exists()).toBe(false)
    expect(wrapper.findAll('.ll-row')).toHaveLength(3)
  })
})
