import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia, type Pinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import TripLogView from '../TripLogView.vue'
import { tripLogApi, type TripLogEntry } from '@/services/tripLogApi'
import { commutePlacesApi } from '@/services/commutePlacesApi'
import { vehicleApi, type Vehicle } from '@/services/vehicleApi'
import { downloadCsv } from '@/utils/download'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { logEntry } from '@/services/__tests__/tripLogFixtures'

vi.mock('@/services/tripLogApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/tripLogApi')>()
  return {
    ...actual,
    tripLogApi: {
      log: vi.fn<typeof actual.tripLogApi.log>(),
      update: vi.fn<typeof actual.tripLogApi.update>(),
      setPurpose: vi.fn<typeof actual.tripLogApi.setPurpose>(),
      setHidden: vi.fn<typeof actual.tripLogApi.setHidden>(),
      mergeWithNext: vi.fn<typeof actual.tripLogApi.mergeWithNext>(),
      resolvePlaces: vi.fn<typeof actual.tripLogApi.resolvePlaces>(),
      editor: vi.fn<typeof actual.tripLogApi.editor>(),
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

vi.mock('@/utils/download', () => ({ downloadCsv: vi.fn<typeof downloadCsv>() }))

// The page looks up addresses in rounds with pauses between them, and stops when it closes; left
// open, a test's page would carry on asking in the tests after it.
enableAutoUnmount(afterEach)

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })
const car = { id: 1, vin: 'FAKEVN00000000001', vehicleType: 'hev' } as Vehicle

// Oldest first, as the API answers; the page lists them newest first.
const trips: TripLogEntry[] = [
  logEntry({ id: 1, startedAt: '2026-10-01T07:00:00Z', purpose: 'business' }),
  logEntry({ id: 2, startedAt: '2026-10-02T07:00:00Z', isHidden: true, notes: 'GPS glitch' }),
  logEntry({ id: 3, startedAt: '2026-10-03T07:00:00Z', purpose: 'private' }),
]

async function mountView() {
  const wrapper = mount(TripLogView, {
    global: {
      plugins: [i18n, pinia],
      stubs: {
        FontAwesomeIcon: true,
        CommutePlacesModal: true,
        TripRouteMap: true,
        StatsChartCard: true,
        RouterLink: true,
        teleport: true,
      },
    },
  })
  await flushPromises()
  return wrapper
}

// The day each listed trip sits under, in the order they are listed.
function listedIds(wrapper: Awaited<ReturnType<typeof mountView>>) {
  return wrapper
    .findAll('.ll-day')
    .flatMap((day) => day.findAll('.trip-log-item').map(() => day.find('.ll-day__title').text()))
}

function flat(text: string) {
  return text.replace(/\s+/g, ' ')
}

function button(wrapper: Awaited<ReturnType<typeof mountView>>, text: string) {
  return wrapper.findAll('button').filter((b) => b.text() === text)
}

// Picks the trip at this place in the list, which opens its detail.
async function pickTrip(wrapper: Awaited<ReturnType<typeof mountView>>, index: number) {
  await wrapper.findAll('.trip-log-item__select')[index]!.trigger('click')
  await flushPromises()
}

// Handed to the mount and to every store the test reads, rather than left to the active pinia,
// which a busy test run has been seen to switch between a mount and its assertions.
let pinia: Pinia

describe('TripLogView', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    vi.clearAllMocks()
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-19T12:00:00Z'))
    vi.mocked(vehicleApi.list).mockResolvedValue([car])
    vi.mocked(commutePlacesApi.list).mockResolvedValue([])
    vi.mocked(tripLogApi.log).mockResolvedValue(trips)
    vi.mocked(tripLogApi.editor).mockResolvedValue({
      id: 1,
      points: [],
      originalPoints: null,
      editedAt: null,
      previousStop: null,
      nextStop: null,
      suggestion: false,
      notBefore: null,
      notAfter: null,
      maxPoints: 2000,
    })
    vi.mocked(tripLogApi.resolvePlaces).mockResolvedValue({
      trips: [],
      available: true,
      hasMore: false,
    })
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it("loads this month's trips for the car, newest first, without the hidden one", async () => {
    const wrapper = await mountView()

    const [vin, from, to] = vi.mocked(tripLogApi.log).mock.calls[0]!
    expect(vin).toBe(car.vin)
    expect(new Date(from).getMonth()).toBe(9)
    expect(new Date(to).getTime()).toBeGreaterThan(new Date(from).getTime())
    expect(listedIds(wrapper)).toEqual(['Sat, 3 Oct 2026', 'Thu, 1 Oct 2026'])
  })

  it('lists the hidden trips once asked to, and counts them on the switch', async () => {
    const wrapper = await mountView()
    const toggle = wrapper.find('.trip-log-hidden-toggle:not(.trip-log-year-toggle)')

    expect(toggle.text()).toBe('Show hidden trip')
    await toggle.find('input').setValue(true)

    expect(listedIds(wrapper)).toEqual(['Sat, 3 Oct 2026', 'Fri, 2 Oct 2026', 'Thu, 1 Oct 2026'])
    expect(wrapper.find('.trip-log-item--hidden').exists()).toBe(true)
  })

  it('leaves the hidden trip out of the totals and the export', async () => {
    const wrapper = await mountView()

    await button(wrapper, 'Export CSV')[0]!.trigger('click')

    const csv = vi.mocked(downloadCsv).mock.calls[0]![1]
    expect(csv).not.toContain('GPS glitch')
    expect(csv.trim().split('\n')).toHaveLength(3)
    expect(wrapper.find('.trip-log-totals').text()).toContain('Unclassified (0)')
    expect(wrapper.find('.ll-tiles').text()).toContain('2')
  })

  it('joins a trip with the next one once confirmed, and offers none for the newest', async () => {
    vi.mocked(tripLogApi.mergeWithNext).mockResolvedValue(logEntry({ id: 1, distanceKm: 80 }))
    const wrapper = await mountView()

    await pickTrip(wrapper, 0)
    expect(button(wrapper, 'Join with next trip')).toHaveLength(0)
    await pickTrip(wrapper, 1)
    await button(wrapper, 'Join with next trip')[0]!.trigger('click')
    expect(tripLogApi.mergeWithNext).not.toHaveBeenCalled()

    await button(wrapper, 'Join trips')[0]!.trigger('click')
    await flushPromises()

    expect(tripLogApi.mergeWithNext).toHaveBeenCalledWith(car.vin, 1)
    expect(button(wrapper, 'Join trips')).toHaveLength(0)
  })

  it('leaves the trips as they are when the join is cancelled', async () => {
    const wrapper = await mountView()

    await pickTrip(wrapper, 1)
    await button(wrapper, 'Join with next trip')[0]!.trigger('click')
    await button(wrapper, 'Cancel')[0]!.trigger('click')

    expect(tripLogApi.mergeWithNext).not.toHaveBeenCalled()
    expect(button(wrapper, 'Join trips')).toHaveLength(0)
  })

  it('hides a trip, which then leaves the list', async () => {
    vi.mocked(tripLogApi.setHidden).mockResolvedValue({ ...trips[2]!, isHidden: true })
    const wrapper = await mountView()

    await pickTrip(wrapper, 0)
    await button(wrapper, 'Hide')[0]!.trigger('click')
    await flushPromises()

    expect(tripLogApi.setHidden).toHaveBeenCalledWith(car.vin, 3, true)
    expect(listedIds(wrapper)).toEqual(['Thu, 1 Oct 2026'])
    expect(wrapper.find('.trip-log-hidden-toggle:not(.trip-log-year-toggle)').text()).toBe(
      'Show 2 hidden trips',
    )
  })

  it('steps to the month before from the calendar, and not past this month', async () => {
    const wrapper = await mountView()
    const [prev, next] = wrapper.findAll('.month-cal__nav')

    expect(next!.attributes('disabled')).toBeDefined()
    await prev!.trigger('click')
    await flushPromises()

    const [, from] = vi.mocked(tripLogApi.log).mock.lastCall!
    expect(new Date(from).getMonth()).toBe(8)
  })

  it('marks the days with trips on the calendar', async () => {
    const wrapper = await mountView()

    const active = wrapper
      .findAll('.month-cal__day--active')
      .map((d) => d.find('.month-cal__num').text())
    expect(active).toEqual(['1', '3'])
  })

  it('loads the whole year when asked to', async () => {
    const wrapper = await mountView()

    await wrapper.find('.trip-log-year-toggle input').setValue(true)
    await flushPromises()

    const [, from, to] = vi.mocked(tripLogApi.log).mock.lastCall!
    expect(new Date(from).getMonth()).toBe(0)
    expect(new Date(to).getFullYear()).toBe(2027)
  })

  it('asks for no addresses while place names are switched off', async () => {
    pinia = createPinia()
    setActivePinia(pinia)
    useUiSettingsStore(pinia).placeNamesEnabled = false

    const wrapper = await mountView()

    expect(tripLogApi.resolvePlaces).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain(en.tripLog.placesOff)
  })

  it('totals all trips and each purpose with its share of the distance', async () => {
    const wrapper = await mountView()
    const totals = flat(wrapper.find('.trip-log-totals').text())

    expect(totals).toContain('All trips (2)85.0 km')
    expect(totals).toContain('Business (1)42.5 km')
    expect(totals).toContain('Private (1)42.5 km')
  })

  it('sums up the driving: time, average and longest trip, average speed', async () => {
    vi.mocked(tripLogApi.log).mockResolvedValue([
      logEntry({ id: 1, startedAt: '2026-10-01T07:00:00Z', endedAt: '2026-10-01T07:34:00Z' }),
      logEntry({ id: 3, startedAt: '2026-10-03T07:00:00Z', endedAt: '2026-10-03T07:34:00Z' }),
    ])
    const wrapper = await mountView()
    const summary = flat(wrapper.find('.trip-log-totals').text())

    // Two trips of 34 minutes and 42.5 km each.
    expect(summary).toContain('1h 8m')
    expect(summary).toContain('42.5 km')
    expect(summary).toContain('75 km/h')
  })

  it('says how far the unclassified trips went', async () => {
    vi.mocked(tripLogApi.log).mockResolvedValue([logEntry({ id: 1 }), logEntry({ id: 2 })])
    const wrapper = await mountView()

    expect(wrapper.find('.trip-log-bulk').text()).toContain(
      '2 trips (85.0 km) have no purpose yet. Mark them all as:',
    )
  })

  it('opens a trip inside the list when picked, and closes it when picked again', async () => {
    const wrapper = await mountView()

    expect(wrapper.find('.trip-log-detail').exists()).toBe(false)
    await pickTrip(wrapper, 1)

    const item = wrapper.findAll('.trip-log-item')[1]!
    expect(item.find('.trip-log-detail').exists()).toBe(true)
    expect(item.classes()).toContain('trip-log-item--selected')
    expect(tripLogApi.editor).toHaveBeenCalledWith(car.vin, 1)

    await pickTrip(wrapper, 1)
    expect(wrapper.find('.trip-log-detail').exists()).toBe(false)
  })
})
