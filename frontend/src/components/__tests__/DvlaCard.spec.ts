import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import type { DvlaInfo } from '@/services/dvlaApi'
import { useDvlaStore } from '@/stores/dvla'
import DvlaCard from '../DvlaCard.vue'

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })
const global = {
  plugins: [i18n],
  stubs: { DetailModal: { template: '<div><slot /></div>' }, FontAwesomeIcon: true },
}

// The DVLA's published example answer, with the tax and MOT a test sets.
function info(overrides: Partial<DvlaInfo> = {}): DvlaInfo {
  return {
    found: true,
    taxStatus: 'Taxed',
    taxDueDate: '2027-08-01',
    taxDaysLeft: 258,
    motStatus: 'No details held by DVLA',
    motExpiryDate: null,
    motDaysLeft: null,
    make: 'ROVER',
    colour: 'BLUE',
    fuelType: 'PETROL',
    record: {
      monthOfFirstRegistration: '2004-12',
      dateOfLastV5CIssued: '2016-12-25',
      co2Emissions: 135,
      engineCapacity: 2494,
      typeApproval: 'N1',
      wheelplan: 'NON STANDARD',
      markedForExport: false,
    },
    fetchedAt: '2026-11-16T11:40:00Z',
    lastError: null,
    ...overrides,
  }
}

function mountCard(lookup: DvlaInfo | null, firstMotDue: string | null = '2029-07-31') {
  const store = useDvlaStore()
  store.available = true
  store.status = { licensePlate: 'AB12CDE', firstMotDue, info: lookup }
  return mount(DvlaCard, { global })
}

describe('DvlaCard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-16T12:00:00Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('shows the tax status, and both dates beneath it', () => {
    const text = mountCard(info()).text()

    expect(text).toContain('Tax and MOT')
    expect(text).toContain('Taxed')
    expect(text).toContain('Tax due 1 Aug 2027 · first MOT due 31 Jul 2029')
  })

  it('counts the time left in months and days', () => {
    const wrapper = mountCard(info())

    expect(wrapper.get('[data-testid="dvla-tax"]').text()).toContain(
      'Due 1 Aug 2027 · 8 mo 16 d left',
    )
    const mot = wrapper.get('[data-testid="dvla-mot"]').text()
    expect(mot).toContain('First MOT due 31 Jul 2029 · 2 yr 8 mo left')
    expect(mot).toContain('Not needed yet')
  })

  it('lists what the DVLA holds, leaving out what it did not send', () => {
    const text = mountCard(info()).text()

    expect(text).toContain('ROVER · Blue · Petrol')
    expect(text).toContain('December 2004')
    expect(text).toContain('25 Dec 2016')
    expect(text).toContain('135 g/km')
    expect(text).toContain('N1 · Non standard')
    expect(text).toContain('Marked for export')
    expect(text).not.toContain('Euro status')
    expect(text).toContain('DVLA Vehicle Enquiry Service · AB12 CDE')
  })

  it('marks an untaxed car and an expired MOT in red', () => {
    const wrapper = mountCard(
      info({
        taxStatus: 'Untaxed',
        taxDueDate: '2026-11-01',
        taxDaysLeft: -15,
        motStatus: 'Not valid',
        motExpiryDate: '2026-10-01',
        motDaysLeft: -46,
      }),
    )

    expect(wrapper.get('[data-testid="dvla-tax"]').text()).toContain('Ran out 1 Nov 2026')
    expect(wrapper.get('[data-testid="dvla-tax"] .badge').classes()).toContain('badge-danger')
    expect(wrapper.get('[data-testid="dvla-mot"]').text()).toContain('Expired')
    expect(wrapper.get('[data-testid="dvla-mot"] .badge').classes()).toContain('badge-danger')
  })

  it('warns when the tax runs out within the month, and shows a valid MOT date', () => {
    const wrapper = mountCard(
      info({
        taxDueDate: '2026-12-01',
        taxDaysLeft: 15,
        motStatus: 'Valid',
        motExpiryDate: '2027-03-04',
        motDaysLeft: 108,
      }),
    )

    expect(wrapper.get('[data-testid="dvla-tax"] .badge').classes()).toContain('badge-warning')
    expect(wrapper.get('[data-testid="dvla-tax"]').text()).toContain('15 days left')
    expect(wrapper.get('[data-testid="dvla-mot"]').text()).toContain('Due 4 Mar 2027')
    expect(wrapper.get('[data-testid="dvla-mot"] .badge').classes()).toContain('badge-success')
  })

  it('says so when the DVLA does not know the plate', () => {
    const text = mountCard({ ...info(), found: false }).text()

    expect(text).toContain('Plate unknown')
    expect(text).toContain('The DVLA knows no vehicle with the plate AB12 CDE')
  })

  it('does not call the plate unknown when the lookup itself failed', () => {
    const text = mountCard({
      ...info(),
      found: false,
      fetchedAt: null,
      lastError: 'The DVLA refused the key (DVLA_API_KEY)',
    }).text()

    expect(text).toContain('Not looked up yet')
    expect(text).not.toContain('Plate unknown')
    expect(text).toContain('The last check with the DVLA failed')
  })
})
