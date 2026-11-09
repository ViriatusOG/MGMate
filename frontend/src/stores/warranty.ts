import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useLoadingTracker } from '@/composables/useLoadingTracker'
import {
  warrantyApi,
  type Warranty,
  type OwnershipDatesRequest,
  type WarrantyCoverRequest,
} from '@/services/warrantyApi'

/** The car's warranty. Every change answers with the whole warranty, which replaces what is held. */
export const useWarrantyStore = defineStore('warranty', () => {
  const warranty = ref<Warranty | null>(null)
  const { loading, withLoading } = useLoadingTracker()
  const loadError = ref<Error | null>(null)
  const actionError = ref<Error | null>(null)

  async function fetch(vin: string) {
    await withLoading(loadError, async () => {
      warranty.value = await warrantyApi.get(vin)
    })
  }

  function act(call: () => Promise<Warranty>) {
    return withLoading(actionError, async () => {
      warranty.value = await call()
    })
  }

  const setDates = (vin: string, req: OwnershipDatesRequest) =>
    act(() => warrantyApi.setDates(vin, req))
  const addCover = (vin: string, req: WarrantyCoverRequest) =>
    act(() => warrantyApi.addCover(vin, req))
  const updateCover = (vin: string, id: number, req: WarrantyCoverRequest) =>
    act(() => warrantyApi.updateCover(vin, id, req))
  const deleteCover = (vin: string, id: number) => act(() => warrantyApi.deleteCover(vin, id))
  const reset = (vin: string) => act(() => warrantyApi.reset(vin))

  return {
    warranty,
    loading,
    loadError,
    actionError,
    fetch,
    setDates,
    addCover,
    updateCover,
    deleteCover,
    reset,
  }
})
