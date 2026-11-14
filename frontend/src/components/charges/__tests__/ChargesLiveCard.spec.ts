import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import type { TelemetrySnapshot, Vehicle } from '@/services/vehicleApi'
import { useVehicleStore } from '@/stores/vehicle'
import ChargesLiveCard from '../ChargesLiveCard.vue'

const send = vi.fn<(...args: unknown[]) => void>()

vi.mock('@/services/activityApi', () => ({
  activityApi: {
    timeline: vi.fn<() => Promise<unknown>>(async () => ({
      soc: [],
      events: [
        {
          kind: 'chargingStarted',
          at: new Date(Date.now() - 3_600_000).toISOString(),
          socPercent: 40,
        },
      ],
    })),
  },
}))

vi.mock('@/composables/useVehicleCommand', async () => {
  const { ref } = await import('vue')
  return {
    useVehicleCommand: () => ({
      sending: ref(null),
      lastResult: ref(null),
      isPending: () => false,
      send,
    }),
  }
})

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

const charging: Partial<TelemetrySnapshot> = {
  isCharging: true,
  evSocPercent: 71,
  socTargetPercent: 80,
  hvPower: -6.8,
  hvVoltage: 386,
  hvCurrent: -18,
  remainingChargingTime: 3240,
}

async function mountWith(status: Partial<TelemetrySnapshot> | null) {
  const store = useVehicleStore()
  store.vehicles = [{ id: 1, vin: 'VIN1' } as Vehicle]
  store.currentStatus = status as TelemetrySnapshot | null
  const wrapper = mount(ChargesLiveCard, {
    global: {
      plugins: [i18n],
      stubs: { FontAwesomeIcon: true, OverviewChart: true, CommandFeedback: true },
    },
  })
  await flushPromises()
  return wrapper
}

describe('ChargesLiveCard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    send.mockClear()
  })

  it('shows nothing while the car is not charging', async () => {
    const wrapper = await mountWith({ ...charging, isCharging: false })
    expect(wrapper.find('.ch-live').exists()).toBe(false)
  })

  it('shows the level, the target, the power and the time left', async () => {
    const wrapper = await mountWith(charging)
    const text = wrapper.text()
    expect(wrapper.find('.ch-live__ring').text()).toContain('71%')
    expect(text).toContain('80%')
    expect(text).toContain('6.8')
    expect(text).toContain('386 V · 18 A')
    expect(text).toContain('54 min')
  })

  it('says where this charge began', async () => {
    const wrapper = await mountWith(charging)
    expect(wrapper.text()).toMatch(/at 40%/)
  })

  it('draws the level since the start and the forecast to the target', async () => {
    const wrapper = await mountWith(charging)
    const chart = wrapper.findComponent({ name: 'OverviewChart' })
    expect(chart.exists()).toBe(true)
  })

  it('asks before stopping, then sends the stop', async () => {
    const wrapper = await mountWith(charging)
    const button = wrapper.find('.ch-live button')
    await button.trigger('click')
    expect(send).not.toHaveBeenCalled()
    await button.trigger('click')
    expect(send).toHaveBeenCalledWith('VIN1', 'charging', 'false', expect.anything())
  })
})
