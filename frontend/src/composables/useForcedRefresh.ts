import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useVehicleCommand } from '@/composables/useVehicleCommand'

/**
 * The one action that wakes the car for fresh data. Waking it costs 12V battery, and the gateway
 * already reads MG's cached status on its own without waking it, so this asks first.
 */
export function useForcedRefresh() {
  const { t } = useI18n()
  const vehicleStore = useVehicleStore()
  const { sending, send } = useVehicleCommand()

  const vin = computed(() => vehicleStore.activeVin)
  const refreshing = computed(() => vehicleStore.loading || sending.value === 'refresh')
  const canRefresh = computed(() => !!vin.value && !refreshing.value)

  function refresh() {
    if (!vin.value || !window.confirm(t('control.refreshConfirm'))) return
    send(vin.value, 'refresh', 'force')
  }

  return { refresh, refreshing, canRefresh }
}
