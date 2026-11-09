import { describe, it, expect, beforeEach, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import MaintenanceItemDetailModal from '../MaintenanceItemDetailModal.vue'
import {
  maintenanceApi,
  type LogMaintenanceServiceResponse,
  type MaintenanceItem,
  type MaintenanceLogEntry,
} from '@/services/maintenanceApi'
import { ApiError } from '@/services/apiCore'
import { maintenanceItem } from '@/services/__tests__/maintenanceFixtures'
import { DetailModalStub } from './stubs'
import { warrantyApi, type Warranty } from '@/services/warrantyApi'

vi.mock('@/services/maintenanceApi', () => ({
  maintenanceApi: {
    list: vi.fn<() => Promise<MaintenanceItem[]>>().mockResolvedValue([]),
    create: vi.fn<() => Promise<MaintenanceItem>>(),
    update: vi.fn<() => Promise<MaintenanceItem>>(),
    delete: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
    listLog: vi.fn<() => Promise<MaintenanceLogEntry[]>>().mockResolvedValue([]),
    logService: vi.fn<() => Promise<LogMaintenanceServiceResponse>>(),
    deleteLogEntry: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
  },
}))

vi.mock('@/services/warrantyApi', () => ({
  warrantyApi: {
    get: vi.fn<() => Promise<Warranty>>(),
    setDates: vi.fn<() => Promise<Warranty>>(),
  },
}))

const warranty = (registrationDate: string | null): Warranty => ({
  registrationDate,
  deliveryDate: '2026-11-07T00:00:00Z',
  deliveryOdometerKm: 24,
  odometerKm: 24,
  covers: [],
})

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

const entry: MaintenanceLogEntry = {
  id: 7,
  maintenanceItemId: 1,
  performedAt: '2026-09-01',
  odometerKm: 12000,
  notes: null,
  createdAt: '2026-09-01',
}

async function openModal(item: MaintenanceItem = maintenanceItem()) {
  const wrapper = mount(MaintenanceItemDetailModal, {
    props: { open: false, vin: 'FAKEVN00000000001', item },
    global: { plugins: [i18n], stubs: { DetailModal: DetailModalStub, FontAwesomeIcon: true } },
  })
  await wrapper.setProps({ open: true })
  await flushPromises()
  return wrapper
}

function button(wrapper: Awaited<ReturnType<typeof openModal>>, label: string) {
  return wrapper.findAll('button').find((b) => b.text() === label)!
}

async function deleteItem(wrapper: Awaited<ReturnType<typeof openModal>>) {
  await button(wrapper, 'Delete').trigger('click')
  await button(wrapper, 'Confirm').trigger('click')
  await flushPromises()
}

describe('MaintenanceItemDetailModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('closes once the item is deleted', async () => {
    const wrapper = await openModal()

    await deleteItem(wrapper)

    expect(maintenanceApi.delete).toHaveBeenCalledWith('FAKEVN00000000001', 1)
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('stays open and says why when the delete is refused', async () => {
    vi.mocked(maintenanceApi.delete).mockRejectedValueOnce(new ApiError(500, 'Server error'))
    const wrapper = await openModal()

    await deleteItem(wrapper)

    expect(wrapper.emitted('close')).toBeUndefined()
    expect(wrapper.find('.text-danger').text()).toBe(en.errors.server)
  })

  // No undo, and it moves the item's last service back: the first tap only asks.
  it('deletes a history entry on the second tap only', async () => {
    vi.mocked(maintenanceApi.listLog).mockResolvedValue([entry])
    vi.mocked(maintenanceApi.deleteLogEntry).mockResolvedValue(undefined)
    const wrapper = await openModal()
    const button = wrapper.find('.maintenance-history__delete')

    await button.trigger('click')
    await flushPromises()
    expect(maintenanceApi.deleteLogEntry).not.toHaveBeenCalled()
    expect(button.classes()).toContain('btn-danger')
    expect(button.attributes('aria-label')).toBe(en.maintenance.confirmDeleteLogEntryLabel)

    await button.trigger('click')
    await flushPromises()
    expect(maintenanceApi.deleteLogEntry).toHaveBeenCalledOnce()
  })

  it('says why when a history entry cannot be deleted', async () => {
    vi.mocked(maintenanceApi.listLog).mockResolvedValue([entry])
    vi.mocked(maintenanceApi.deleteLogEntry).mockRejectedValueOnce(new TypeError('offline'))
    const wrapper = await openModal()

    await wrapper.find('.maintenance-history__delete').trigger('click')
    await wrapper.find('.maintenance-history__delete').trigger('click')
    await flushPromises()

    expect(wrapper.find('.text-danger').text()).toBe(en.errors.offline)
    expect(wrapper.findAll('.maintenance-history__row')).toHaveLength(1)
  })

  describe('with nothing to count from', () => {
    it('offers the registration date first, and logs a past service on request', async () => {
      const wrapper = await openModal()

      expect(wrapper.find('#count-from-registration').exists()).toBe(true)
      expect(wrapper.find('#log-performed-at').exists()).toBe(false)

      await button(wrapper, en.maintenance.countFrom.pastService).trigger('click')

      expect(wrapper.find('#count-from-registration').exists()).toBe(false)
      expect(wrapper.find('#log-performed-at').exists()).toBe(true)
    })

    it('saves the registration date for the car, keeping its delivery', async () => {
      vi.mocked(warrantyApi.get).mockResolvedValue(warranty(null))
      vi.mocked(warrantyApi.setDates).mockResolvedValue(warranty('2026-07-31T00:00:00Z'))
      const wrapper = await openModal()

      await wrapper.find('#count-from-registration').setValue('2026-07-31')
      await wrapper.find<HTMLInputElement>('#count-from-registration').element.form!.requestSubmit()
      await flushPromises()

      expect(warrantyApi.setDates).toHaveBeenCalledWith('FAKEVN00000000001', {
        registrationDate: '2026-07-31',
        deliveryDate: '2026-11-07',
        deliveryOdometerKm: 24,
      })
    })

    it('says why when the date is refused', async () => {
      vi.mocked(warrantyApi.get).mockResolvedValue(warranty(null))
      vi.mocked(warrantyApi.setDates).mockRejectedValueOnce(new TypeError('offline'))
      const wrapper = await openModal()

      await wrapper.find('#count-from-registration').setValue('2026-07-31')
      await wrapper.find<HTMLInputElement>('#count-from-registration').element.form!.requestSubmit()
      await flushPromises()

      expect(wrapper.find('[role="alert"]').text()).toBe(en.errors.offline)
    })
  })

  it('only offers to log a service once the item counts from something', async () => {
    const wrapper = await openModal(
      maintenanceItem({ countingFrom: 'registration', countingFromDate: '2026-07-31T00:00:00Z' }),
    )

    expect(wrapper.find('#count-from-registration').exists()).toBe(false)
    expect(wrapper.find('#log-performed-at').exists()).toBe(true)
  })
})
