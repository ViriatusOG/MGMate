import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import type { MaintenanceItem } from '@/services/maintenanceApi'
import type { TelemetrySnapshot } from '@/services/vehicleApi'
import { maintenanceItem } from '@/services/__tests__/maintenanceFixtures'
import { useMaintenanceStore } from '@/stores/maintenance'
import { useVehicleStore } from '@/stores/vehicle'
import NeedsAttention from '../NeedsAttention.vue'

vi.mock('@/composables/useTyrePressureThresholds', async (importOriginal) => {
  const real = await importOriginal<typeof import('@/composables/useTyrePressureThresholds')>()
  const { ref } = await import('vue')
  return {
    ...real,
    useTyrePressureThresholds: () => ref(real.DEFAULT_TYRE_PRESSURE_THRESHOLDS),
  }
})

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

function mountWith(items: MaintenanceItem[], status: Partial<TelemetrySnapshot> | null = null) {
  const maintenance = useMaintenanceStore()
  maintenance.items = items
  // The component asks for the items itself; here they are already in the store.
  maintenance.fetchItems = vi.fn<typeof maintenance.fetchItems>()
  useVehicleStore().currentStatus = status as TelemetrySnapshot | null
  return mount(NeedsAttention, {
    global: {
      plugins: [i18n],
      stubs: { FontAwesomeIcon: true, RouterLink: { template: '<a><slot /></a>' } },
    },
  })
}

describe('NeedsAttention', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('is not there when nothing needs doing', () => {
    const wrapper = mountWith([maintenanceItem()], { batteryVoltage: 12.6 })

    expect(wrapper.find('.ov-attention').exists()).toBe(false)
  })

  it('lists overdue and due-soon service, overdue in red', () => {
    const wrapper = mountWith([
      maintenanceItem({ id: 1, name: 'Annual service', dueStatus: 'overdue' }),
      maintenanceItem({ id: 2, name: 'Tyre rotation', dueStatus: 'dueSoon' }),
    ])

    const items = wrapper.findAll('.ov-attention__item')
    expect(items.map((i) => i.find('b').text())).toEqual([
      'Annual service is overdue',
      'Tyre rotation is due soon',
    ])
    expect(items[0]!.classes()).toContain('ov-attention__item--danger')
  })

  it('warns about a low 12V battery and a soft tyre', () => {
    const wrapper = mountWith([], {
      batteryVoltage: 11.8,
      tyrePressureFrontLeft: 2.5,
      tyrePressureFrontRight: 1.6,
      tyrePressureRearLeft: 2.5,
      tyrePressureRearRight: 2.5,
    })

    const text = wrapper.text()
    expect(text).toContain('12V battery is low')
    expect(text).toContain('A tyre is low')
    expect(text).toContain('Front right')
  })
})
