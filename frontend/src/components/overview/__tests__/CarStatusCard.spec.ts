import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import type { TelemetrySnapshot } from '@/services/vehicleApi'
import { useVehicleStore } from '@/stores/vehicle'
import { useUiSettingsStore } from '@/stores/settingsUi'
import CarStatusCard from '../CarStatusCard.vue'

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

function mountWith(status: Partial<TelemetrySnapshot> | null, region = 'NL') {
  useUiSettingsStore().region = region
  useVehicleStore().currentStatus = status as TelemetrySnapshot | null
  return mount(CarStatusCard, {
    props: { hasControls: true },
    global: { plugins: [i18n], stubs: { FontAwesomeIcon: true } },
  })
}

function row(wrapper: ReturnType<typeof mountWith>, label: string) {
  const item = wrapper.findAll('.ov-status__list > div').find((d) => d.find('dt').text() === label)
  return item?.find('dd')
}

describe('CarStatusCard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('is not there before the car has reported', () => {
    expect(mountWith(null).find('.ov-status').exists()).toBe(false)
  })

  it('says all is shut and locked, and leaves out what the car does not report', () => {
    const wrapper = mountWith(closed)

    expect(row(wrapper, 'Locks')?.text()).toBe('Locked')
    expect(row(wrapper, 'Doors')?.text()).toBe('Closed')
    expect(row(wrapper, 'Windows')?.text()).toBe('Closed')
    expect(row(wrapper, 'Charge cable')?.text()).toBe('Not plugged in')
    expect(row(wrapper, 'Sunroof')).toBeUndefined()
    expect(wrapper.findAll('.ov-topview .is-open')).toHaveLength(0)
  })

  it('swings the driver door out on the right of a British car and on the left elsewhere', () => {
    const open = { ...closed, isLocked: false, driverDoorOpen: true }

    const gb = mountWith(open, 'GB')
    expect(gb.find('.ov-topview__door--right.is-open').exists()).toBe(true)
    expect(gb.find('.ov-topview__door--left.is-open').exists()).toBe(false)
    expect(row(gb, 'Doors')?.text()).toBe('Driver open')
    expect(row(gb, 'Locks')?.classes()).toContain('is-warn')

    setActivePinia(createPinia())
    const nl = mountWith(open, 'NL')
    expect(nl.find('.ov-topview__door--left.is-open').exists()).toBe(true)
  })

  it('marks open windows, the boot and a charging cable', () => {
    const wrapper = mountWith({
      ...closed,
      rearLeftWindowOpen: true,
      passengerWindowOpen: true,
      trunkOpen: true,
      chargerConnected: true,
      isCharging: true,
    })

    expect(row(wrapper, 'Windows')?.text()).toBe('2 open')
    expect(row(wrapper, 'Boot')?.text()).toBe('Open')
    expect(wrapper.findAll('.ov-topview__window.is-open')).toHaveLength(2)
    expect(row(wrapper, 'Charge cable')?.text()).toBe('Charging')
    expect(wrapper.find('.ov-topview__port.is-charging').exists()).toBe(true)
  })

  it('marks an open bonnet and only draws a sunroof when one is reported open', () => {
    const shut = mountWith(closed)
    expect(shut.find('.ov-topview__lid.is-open').exists()).toBe(false)

    setActivePinia(createPinia())
    const open = mountWith({ ...closed, bonnetOpen: true, sunRoofOpen: true })
    expect(open.findAll('.ov-topview__lid.is-open')).toHaveLength(1)
    expect(open.findAll('.ov-topview .is-open')).toHaveLength(2)
    expect(row(open, 'Bonnet')?.text()).toBe('Open')
  })

  it('asks for the controls when its link is pressed', async () => {
    const wrapper = mountWith(closed)
    await wrapper.find('.ov-status__link').trigger('click')
    expect(wrapper.emitted('controls')).toHaveLength(1)
  })
})
