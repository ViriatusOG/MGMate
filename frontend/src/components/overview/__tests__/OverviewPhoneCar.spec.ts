import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import type { TelemetrySnapshot } from '@/services/vehicleApi'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import type { TyrePressures } from '@/components/carDiagram/useTyreDisplay'
import OverviewPhoneCar from '../OverviewPhoneCar.vue'

vi.mock('@/composables/useTyrePressureThresholds', async (importOriginal) => {
  const real = await importOriginal<typeof import('@/composables/useTyrePressureThresholds')>()
  const { ref } = await import('vue')
  return {
    ...real,
    useTyrePressureThresholds: () => ref(real.DEFAULT_TYRE_PRESSURE_THRESHOLDS),
  }
})

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

const closed: Partial<TelemetrySnapshot> = {
  isLocked: true,
  driverDoorOpen: false,
  passengerDoorOpen: false,
  rearLeftDoorOpen: false,
  rearRightDoorOpen: false,
  driverWindowOpen: false,
  passengerWindowOpen: false,
  rearLeftWindowOpen: false,
  rearRightWindowOpen: false,
  trunkOpen: false,
  bonnetOpen: false,
  sunRoofOpen: null,
  chargerConnected: false,
  isCharging: false,
}

const pressures: TyrePressures = { frontLeft: 2.5, frontRight: 2.5, rearLeft: 2.5, rearRight: 2.5 }
const noPressures: TyrePressures = {
  frontLeft: null,
  frontRight: null,
  rearLeft: null,
  rearRight: null,
}

function mountWith(status: Partial<TelemetrySnapshot> | null, tyres = pressures) {
  useUiSettingsStore().region = 'GB'
  useVehicleStore().currentStatus = status as TelemetrySnapshot | null
  return mount(OverviewPhoneCar, {
    props: { pressures: tyres },
    global: { plugins: [i18n], stubs: { FontAwesomeIcon: true } },
  })
}

function labels(wrapper: ReturnType<typeof mountWith>) {
  return wrapper.findAll('.ov-pcar__list dt').map((d) => d.text())
}

describe('OverviewPhoneCar', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('is not there before the car has reported', () => {
    expect(mountWith(null).find('.ov-pcar').exists()).toBe(false)
  })

  it('keeps the list short and the drawing clean when all is shut', () => {
    const wrapper = mountWith(closed)

    expect(labels(wrapper)).toEqual(['Doors', 'Windows', 'Boot and bonnet', 'Cable'])
    expect(wrapper.findAll('.ov-pcar__mark')).toHaveLength(0)
    expect(wrapper.find('.ov-pcar__lock').text()).toBe('Locked')
    expect(wrapper.find('.ov-pill').text()).toBe('Parked')
  })

  it('numbers what is open on the drawing and in the list alike', () => {
    const wrapper = mountWith({
      ...closed,
      rearLeftDoorOpen: true,
      driverWindowOpen: true,
      trunkOpen: true,
      chargerConnected: true,
      isCharging: true,
    })

    expect(labels(wrapper)).toEqual([
      '1Rear left door',
      '2Driver window',
      '3Boot',
      'Bonnet',
      '4Cable',
    ])
    expect(wrapper.findAll('.ov-pcar__mark').map((m) => m.text())).toEqual(['1', '2', '3', '4'])
    expect(wrapper.find('.ov-pill').text()).toBe('Charging')
  })

  it('shows the four tyre pressures, and no strip when the car reports none', () => {
    expect(mountWith(closed).findAll('.ov-pcar__tyre')).toHaveLength(4)

    setActivePinia(createPinia())
    expect(mountWith(closed, noPressures).find('.ov-pcar__tyres').exists()).toBe(false)
  })
})
