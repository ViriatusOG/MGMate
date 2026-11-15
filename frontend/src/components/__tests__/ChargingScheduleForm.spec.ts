import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import type { SendOptions } from '@/composables/useVehicleCommand'
import type { TelemetrySnapshot } from '@/services/vehicleApi'
import { setRegionalFormat } from '@/utils/format'
import ChargingScheduleForm from '../ChargingScheduleForm.vue'
import BatteryHeatingScheduleForm from '../BatteryHeatingScheduleForm.vue'

const send =
  vi.fn<
    (vin: string | null, command: string, value: string, options?: SendOptions) => Promise<boolean>
  >()

vi.mock('@/composables/useVehicleCommand', () => ({
  useVehicleCommand: () => ({
    sending: ref(null),
    lastResult: ref(null),
    isPending: () => false,
    send,
    waitUntilSettled: () => Promise.resolve(),
  }),
}))

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })
const global = { plugins: [i18n], stubs: { FontAwesomeIcon: true } }
const VIN = 'FAKEVN00000000001'

function mountCharging(props: Record<string, unknown> = {}) {
  return mount(ChargingScheduleForm, {
    props: {
      vin: VIN,
      mode: 'UNTIL_CONFIGURED_TIME',
      startTime: '23:00',
      endTime: '07:00',
      canChargeUntilTarget: false,
      ...props,
    },
    global,
  })
}

function mountHeating(props: Record<string, unknown> = {}) {
  return mount(BatteryHeatingScheduleForm, {
    props: { vin: VIN, mode: 'off', startTime: '06:00', ...props },
    global,
  })
}

function saveButton(wrapper: ReturnType<typeof mount>) {
  return wrapper.findAll('button').find((b) => b.text() === 'Save schedule')!
}

// The command and payload of the last send, and what it would take as done.
function lastSend() {
  const [, command, value, options] = send.mock.lastCall!
  return {
    command,
    payload: JSON.parse(value) as Record<string, string>,
    confirmedBy: (fields: Partial<TelemetrySnapshot>) =>
      options?.isConfirmed?.(fields as TelemetrySnapshot),
  }
}

beforeEach(() => {
  send.mockReset()
  send.mockResolvedValue(true)
  setRegionalFormat({ region: null, hourCycle: 'h23' })
})

afterEach(() => setRegionalFormat({ region: null, hourCycle: 'h23' }))

describe('ChargingScheduleForm', () => {
  it("starts from the car's schedule, with nothing to save yet", () => {
    const wrapper = mountCharging()

    expect(wrapper.find<HTMLSelectElement>('#charging-schedule-mode').element.value).toBe(
      'UNTIL_CONFIGURED_TIME',
    )
    expect(wrapper.find<HTMLSelectElement>('#charging-schedule-start').element.value).toBe('23')
    expect(wrapper.find<HTMLSelectElement>('#charging-schedule-end').element.value).toBe('7')
    expect(saveButton(wrapper).attributes('disabled')).toBeDefined()
  })

  it('offers charging until the target only on a car that takes one', () => {
    const options = (wrapper: ReturnType<typeof mountCharging>) =>
      wrapper.findAll('#charging-schedule-mode option').map((o) => o.attributes('value'))

    expect(options(mountCharging())).toEqual(['UNTIL_CONFIGURED_TIME', 'DISABLED'])
    expect(options(mountCharging({ canChargeUntilTarget: true }))).toEqual([
      'UNTIL_CONFIGURED_TIME',
      'UNTIL_CONFIGURED_SOC',
      'DISABLED',
    ])
  })

  it('sends a new window, taken as done once the car reports it', async () => {
    const wrapper = mountCharging()

    await wrapper.find('#charging-schedule-start').setValue('1')
    await saveButton(wrapper).trigger('click')

    const sent = lastSend()
    expect(sent.command).toBe('scheduled-charging')
    expect(sent.payload).toEqual({
      mode: 'UNTIL_CONFIGURED_TIME',
      startTime: '01:00',
      endTime: '07:00',
    })
    const reported = {
      chargingScheduleMode: 'UNTIL_CONFIGURED_TIME',
      chargingScheduleStartTime: '01:00',
      chargingScheduleEndTime: '07:00',
    }
    expect(sent.confirmedBy({ ...reported, chargingScheduleStartTime: '23:00' })).toBe(false)
    expect(sent.confirmedBy(reported)).toBe(true)
  })

  // The gateway reads both times even to switch the schedule off.
  it('switches the schedule off with its times still in', async () => {
    const wrapper = mountCharging()

    await wrapper.find('#charging-schedule-mode').setValue('DISABLED')
    expect(wrapper.find('#charging-schedule-start').exists()).toBe(false)
    await saveButton(wrapper).trigger('click')

    const sent = lastSend()
    expect(sent.payload).toEqual({ mode: 'DISABLED', startTime: '23:00', endTime: '07:00' })
    expect(sent.confirmedBy({ chargingScheduleMode: 'DISABLED' })).toBe(true)
  })

  it('has no target slider unless given the target', () => {
    expect(mountCharging({ canChargeUntilTarget: true }).find('input[type="range"]').exists()).toBe(
      false,
    )
    expect(
      mountCharging({ canChargeUntilTarget: true, target: 80 })
        .find('input[type="range"]')
        .exists(),
    ).toBe(true)
  })

  it('sends only the target when only the target changed', async () => {
    const wrapper = mountCharging({ canChargeUntilTarget: true, target: 80 })

    await wrapper.find('input[type="range"]').setValue('90')
    await saveButton(wrapper).trigger('click')

    expect(send).toHaveBeenCalledTimes(1)
    const [, command, value, options] = send.mock.lastCall!
    expect(command).toBe('charge-target')
    expect(value).toBe('90')
    expect(options?.isConfirmed?.({ socTargetPercent: 90 } as TelemetrySnapshot)).toBe(true)
  })

  it('sends the schedule first, then the target, when both changed', async () => {
    const wrapper = mountCharging({ canChargeUntilTarget: true, target: 80 })

    await wrapper.find('#charging-schedule-start').setValue('1')
    await wrapper.find('input[type="range"]').setValue('100')
    await saveButton(wrapper).trigger('click')
    await flushPromises()

    expect(send.mock.calls.map((c) => c[1])).toEqual(['scheduled-charging', 'charge-target'])
  })

  it('does not send the target when the schedule was refused', async () => {
    send.mockResolvedValueOnce(false)
    const wrapper = mountCharging({ canChargeUntilTarget: true, target: 80 })

    await wrapper.find('#charging-schedule-start').setValue('1')
    await wrapper.find('input[type="range"]').setValue('100')
    await saveButton(wrapper).trigger('click')
    await flushPromises()

    expect(send.mock.calls.map((c) => c[1])).toEqual(['scheduled-charging'])
  })

  it('reads a schedule the car never reported as off', () => {
    const wrapper = mountCharging({ mode: null, startTime: null, endTime: null })

    expect(wrapper.find<HTMLSelectElement>('#charging-schedule-mode').element.value).toBe(
      'DISABLED',
    )
    expect(saveButton(wrapper).attributes('disabled')).toBeDefined()
  })
})

describe('BatteryHeatingScheduleForm', () => {
  it('switches the schedule on at a time, taken as done once the car reports it', async () => {
    const wrapper = mountHeating()

    expect(wrapper.find('#heating-schedule-start').exists()).toBe(false)
    await wrapper.find('input[role="switch"]').trigger('change')
    await wrapper.find('#heating-schedule-start').setValue('5')
    await saveButton(wrapper).trigger('click')

    const sent = lastSend()
    expect(sent.command).toBe('battery-heating-schedule')
    expect(sent.payload).toEqual({ mode: 'on', startTime: '05:00' })
    expect(
      sent.confirmedBy({
        batteryHeatingScheduleMode: 'on',
        batteryHeatingScheduleStartTime: '06:00',
      }),
    ).toBe(false)
    expect(
      sent.confirmedBy({
        batteryHeatingScheduleMode: 'on',
        batteryHeatingScheduleStartTime: '05:00',
      }),
    ).toBe(true)
  })

  // The gateway reads the start time even to switch the schedule off.
  it('switches the schedule off with its time still in', async () => {
    const wrapper = mountHeating({ mode: 'on', startTime: '06:30' })

    expect(saveButton(wrapper).attributes('disabled')).toBeDefined()
    await wrapper.find('input[role="switch"]').trigger('change')
    await saveButton(wrapper).trigger('click')

    const sent = lastSend()
    expect(sent.payload).toEqual({ mode: 'off', startTime: '06:30' })
    expect(sent.confirmedBy({ batteryHeatingScheduleMode: 'off' })).toBe(true)
  })
})
