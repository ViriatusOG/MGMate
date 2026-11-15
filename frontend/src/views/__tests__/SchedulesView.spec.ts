import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import SchedulesView from '../SchedulesView.vue'
import { climateScheduleApi, type ClimateSchedule } from '@/services/climateScheduleApi'
import { vehicleApi, type TelemetrySnapshot, type Vehicle } from '@/services/vehicleApi'
import { schedule } from '@/services/__tests__/climateScheduleFixtures'

vi.mock('@/services/climateScheduleApi', () => ({
  climateScheduleApi: { list: vi.fn<() => Promise<ClimateSchedule[]>>() },
}))

vi.mock('@/services/vehicleApi', () => ({
  vehicleApi: {
    list: vi.fn<() => Promise<Vehicle[]>>(),
    status: vi.fn<() => Promise<TelemetrySnapshot>>(),
  },
}))

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

const car = { id: 1, vin: 'FAKEVN00000000001', vehicleType: 'bev' } as Vehicle

async function mountView() {
  const wrapper = mount(SchedulesView, {
    global: {
      plugins: [i18n],
      stubs: {
        FontAwesomeIcon: true,
        ClimateScheduleFormModal: true,
        ChargingScheduleForm: true,
        BatteryHeatingScheduleForm: true,
        StatusCard: {
          props: ['label', 'value', 'variant'],
          template:
            '<div class="schedule" :data-variant="variant ?? \'none\'">{{ label }}|{{ value }}</div>',
        },
      },
    },
  })
  await flushPromises()
  return wrapper
}

describe('SchedulesView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    vi.mocked(vehicleApi.list).mockResolvedValue([car])
    vi.mocked(climateScheduleApi.list).mockResolvedValue([])
    vi.mocked(vehicleApi.status).mockResolvedValue({} as TelemetrySnapshot)
  })

  it("lists the car's schedules by name and days, with their start time", async () => {
    vi.mocked(climateScheduleApi.list).mockResolvedValue([
      schedule({ name: 'Work', startTime: '07:30' }),
    ])

    const wrapper = await mountView()

    expect(climateScheduleApi.list).toHaveBeenCalledWith('FAKEVN00000000001')
    const row = wrapper.get('.schedule').text()
    expect(row.startsWith('Work · ')).toBe(true)
    expect(row).toContain('07:30')
  })

  // A schedule whose last run went wrong stands out; one switched off does not compete for it.
  it('marks a schedule whose last run needs a look, and leaves one that is off plain', async () => {
    vi.mocked(climateScheduleApi.list).mockResolvedValue([
      schedule({ id: 1, name: 'Fine', lastRunOutcome: 'started' }),
      schedule({ id: 2, name: 'Refused', lastRunOutcome: 'failed' }),
      schedule({ id: 3, name: 'Off', enabled: false, lastRunOutcome: 'failed' }),
    ])

    const wrapper = await mountView()

    expect(wrapper.findAll('.schedule').map((s) => s.attributes('data-variant'))).toEqual([
      'info',
      'warning',
      'none',
    ])
  })

  it('says so when there are no schedules yet', async () => {
    const wrapper = await mountView()

    expect(wrapper.text()).toContain(en.climateSchedules.empty)
  })

  it('only offers to add a schedule once a car is known', async () => {
    vi.mocked(vehicleApi.list).mockResolvedValue([])

    const wrapper = await mountView()

    expect(wrapper.get('.schedules-section__head button').attributes('disabled')).toBeDefined()
    expect(climateScheduleApi.list).not.toHaveBeenCalled()
  })

  // The schedules the car keeps itself come from its last reported state, so reading them never
  // wakes it.
  it("shows the car's own charging window, target and current, and its battery heating time", async () => {
    vi.mocked(vehicleApi.status).mockResolvedValue({
      chargingScheduleMode: 'UNTIL_CONFIGURED_TIME',
      chargingScheduleStartTime: '23:30:00',
      chargingScheduleEndTime: '05:30:00',
      socTargetPercent: 80,
      chargeCurrentLimit: 'Max',
      batteryHeatingScheduleMode: 'on',
      batteryHeatingScheduleStartTime: '06:45:00',
    } as TelemetrySnapshot)

    const wrapper = await mountView()

    const [charging, heating] = wrapper.findAll('.car-schedule')
    expect(charging!.get('.car-schedule__time').text()).toBe('23:30 – 05:30')
    expect(charging!.text()).toContain(en.schedules.charging.untilTimeLine)
    expect(charging!.text()).toContain('80%')
    expect(charging!.text()).toContain('Max')
    expect(heating!.get('.car-schedule__time').text()).toBe('06:45')
    expect(vehicleApi.status).toHaveBeenCalledWith('FAKEVN00000000001')
  })

  it('shows a charging schedule that stops at the target by its start and the target', async () => {
    vi.mocked(vehicleApi.status).mockResolvedValue({
      chargingScheduleMode: 'UNTIL_CONFIGURED_SOC',
      chargingScheduleStartTime: '01:00',
      chargingScheduleEndTime: '07:00',
      socTargetPercent: 90,
    } as TelemetrySnapshot)

    const wrapper = await mountView()

    const charging = wrapper.findAll('.car-schedule')[0]!
    expect(charging.get('.car-schedule__time').text()).toBe('01:00')
    expect(charging.text()).toContain('Charges until 90%.')
  })

  it('says charging starts on plugging in when the schedule is off', async () => {
    vi.mocked(vehicleApi.status).mockResolvedValue({
      chargingScheduleMode: 'DISABLED',
    } as TelemetrySnapshot)

    const wrapper = await mountView()

    const charging = wrapper.findAll('.car-schedule')[0]!
    expect(charging.classes()).toContain('car-schedule--off')
    expect(charging.text()).toContain(en.schedules.charging.offLine)
  })

  it('leaves out charging and battery heating for a car that does not plug in', async () => {
    vi.mocked(vehicleApi.list).mockResolvedValue([{ ...car, vehicleType: 'hev' } as Vehicle])

    const wrapper = await mountView()

    expect(wrapper.find('.car-schedule').exists()).toBe(false)
    expect(wrapper.text()).toContain(en.schedules.climate.title)
  })
})
