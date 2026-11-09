import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import WarrantySection from '../WarrantySection.vue'
import { warrantyApi, type Warranty, type WarrantyCover } from '@/services/warrantyApi'

vi.mock('@/services/warrantyApi', () => ({
  warrantyApi: {
    get: vi.fn<() => Promise<Warranty>>(),
    setDates: vi.fn<() => Promise<Warranty>>(),
  },
}))

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })
const MILE = 1.609344

function cover(over: Partial<WarrantyCover>): WarrantyCover {
  return {
    id: 1,
    name: 'Cover',
    notes: null,
    months: 12,
    distanceKm: null,
    featured: false,
    sortOrder: 0,
    status: 'active',
    endsOn: '2027-07-31T00:00:00Z',
    daysRemaining: 264,
    kmRemaining: null,
    timeUsed: 0.3,
    distanceUsed: null,
    endedBy: null,
    ...over,
  }
}

function warranty(over: Partial<Warranty> = {}): Warranty {
  return {
    registrationDate: '2026-07-31T00:00:00Z',
    deliveryDate: '2026-11-07T00:00:00Z',
    deliveryOdometerKm: 15 * MILE,
    odometerKm: 58 * MILE,
    covers: [],
    ...over,
  }
}

async function mountSection() {
  const wrapper = mount(WarrantySection, {
    props: { vin: 'FAKEVN00000000001' },
    global: { plugins: [i18n], stubs: { FontAwesomeIcon: true, WarrantyEditModal: true } },
  })
  await flushPromises()
  return wrapper
}

describe('WarrantySection', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('asks for the registration date before anything else', async () => {
    vi.mocked(warrantyApi.get).mockResolvedValue(warranty({ registrationDate: null }))
    vi.mocked(warrantyApi.setDates).mockResolvedValue(warranty())

    const wrapper = await mountSection()
    expect(wrapper.text()).toContain(en.warranty.setup.title)

    await wrapper.find('input[type="date"]').setValue('2026-07-31')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(warrantyApi.setDates).toHaveBeenCalledWith('FAKEVN00000000001', {
      registrationDate: '2026-07-31',
      deliveryDate: '2026-11-07',
      deliveryOdometerKm: 15 * MILE,
    })
  })

  it('shows featured covers as cards and the rest in the table, expired last', async () => {
    vi.mocked(warrantyApi.get).mockResolvedValue(
      warranty({
        covers: [
          cover({
            id: 1,
            name: 'Vehicle',
            featured: true,
            months: 84,
            distanceKm: 80_000 * MILE,
            kmRemaining: 79_942 * MILE,
            distanceUsed: 0.001,
          }),
          cover({
            id: 2,
            name: 'Glass crack',
            months: 3,
            status: 'expired',
            endedBy: 'time',
            endsOn: '2026-10-31T00:00:00Z',
            daysRemaining: -9,
          }),
          cover({ id: 3, name: 'Paint', months: 36, distanceKm: 60_000 * MILE }),
        ],
      }),
    )

    const wrapper = await mountSection()

    expect(wrapper.find('.warranty__featured').text()).toContain('Vehicle')
    expect(wrapper.find('.warranty__featured').text()).toContain('128,748 km')
    const rows = wrapper.findAll('.warranty-table tbody tr').map((r) => r.text())
    expect(rows[0]).toContain('Paint')
    expect(rows[0]).toContain('3 years / 96,561 km')
    expect(rows[1]).toContain('Glass crack')
    expect(rows[1]).toContain('Ended 31 Oct 2026')
    expect(rows[1]).toContain(en.warranty.status.expired)
  })
})
