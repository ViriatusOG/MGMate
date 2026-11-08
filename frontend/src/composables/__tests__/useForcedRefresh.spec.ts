import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { useForcedRefresh } from '../useForcedRefresh'

const vehicle = vi.hoisted(() => ({ activeVin: null as string | null }))
const sendCommand = vi.hoisted(() => vi.fn<(vin: string, command: string, value: string) => void>())

vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: (key: string) => key }) }))

vi.mock('@/stores/vehicle', () => ({
  useVehicleStore: () => ({ loading: false, activeVin: vehicle.activeVin }),
}))

vi.mock('@/composables/useVehicleCommand', () => ({
  useVehicleCommand: () => ({ sending: ref(null), send: sendCommand }),
}))

describe('useForcedRefresh', () => {
  beforeEach(() => {
    vehicle.activeVin = null
    sendCommand.mockClear()
    vi.restoreAllMocks()
  })

  // Waking the car costs 12V battery, so the forced refresh asks first.
  it('wakes the car only after the owner confirms', () => {
    vehicle.activeVin = 'VIN1'
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const { refresh } = useForcedRefresh()

    refresh()
    expect(confirm).toHaveBeenCalledWith('control.refreshConfirm')
    expect(sendCommand).not.toHaveBeenCalled()

    confirm.mockReturnValue(true)
    refresh()
    expect(sendCommand).toHaveBeenCalledWith('VIN1', 'refresh', 'force')
  })

  it('cannot refresh without a car', () => {
    const confirm = vi.spyOn(window, 'confirm')
    const { refresh, canRefresh } = useForcedRefresh()

    expect(canRefresh.value).toBe(false)
    refresh()
    expect(confirm).not.toHaveBeenCalled()
  })
})
