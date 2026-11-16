import { defineStore } from 'pinia'
import { computed, ref, shallowRef } from 'vue'
import { ApiError } from '@/services/apiCore'
import { DVLA_DISABLED_CODE, dvlaApi, type DvlaStatus } from '@/services/dvlaApi'

/**
 * What the DVLA says about the car's UK registration: road tax and MOT. Whether the deployment
 * has a DVLA key at all is only known from the API's answer: without one it says dvla.disabled,
 * and then the card and the notification type stay out of sight.
 */
export const useDvlaStore = defineStore('dvla', () => {
  /** Null until the API has answered. */
  const available = ref<boolean | null>(null)
  const status = shallowRef<DvlaStatus | null>(null)
  const error = ref<Error | null>(null)
  let inFlight: Promise<void> | null = null

  /**
   * Reads the saved lookup again. Once the deployment said it has no key, it is not asked again.
   */
  async function fetchStatus(vin: string) {
    if (available.value === false) return
    if (inFlight) return inFlight
    inFlight = (async () => {
      try {
        status.value = await dvlaApi.get(vin)
        available.value = true
        error.value = null
      } catch (e) {
        if (e instanceof ApiError && e.code === DVLA_DISABLED_CODE) {
          available.value = false
          status.value = null
        } else {
          error.value = e as Error
        }
      } finally {
        inFlight = null
      }
    })()
    return inFlight
  }

  /** Reads the status the first time anything needs it. */
  async function ensureLoaded(vin: string) {
    if (available.value === null && !status.value) await fetchStatus(vin)
  }

  /** Whether the card has anything to show: the deployment has a key and the car a plate. */
  const hasPlate = computed(() => available.value === true && !!status.value?.licensePlate)

  return { available, status, error, hasPlate, fetchStatus, ensureLoaded }
})
