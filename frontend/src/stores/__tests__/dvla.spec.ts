import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useDvlaStore } from '@/stores/dvla'
import { ApiError } from '@/services/apiCore'
import type { DvlaStatus } from '@/services/dvlaApi'

vi.mock('@/services/dvlaApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/services/dvlaApi')>()),
  dvlaApi: { get: vi.fn<() => Promise<DvlaStatus>>() },
}))

const withPlate: DvlaStatus = { licensePlate: 'AB12CDE', firstMotDue: null, info: null }

describe('useDvlaStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('learns that the deployment has a DVLA key from the first answer', async () => {
    const { dvlaApi } = await import('@/services/dvlaApi')
    vi.mocked(dvlaApi.get).mockResolvedValue(withPlate)
    const store = useDvlaStore()

    await store.fetchStatus('VIN1')

    expect(store.available).toBe(true)
    expect(store.hasPlate).toBe(true)
  })

  it('treats dvla.disabled as no key, and does not ask again', async () => {
    const { dvlaApi } = await import('@/services/dvlaApi')
    vi.mocked(dvlaApi.get).mockRejectedValue(
      new ApiError(404, '/api/vehicles/VIN1/dvla', { code: 'dvla.disabled' }),
    )
    const store = useDvlaStore()

    await store.fetchStatus('VIN1')
    await store.fetchStatus('VIN1')

    expect(store.available).toBe(false)
    expect(store.hasPlate).toBe(false)
    expect(dvlaApi.get).toHaveBeenCalledTimes(1)
  })

  it('keeps asking after any other failure', async () => {
    const { dvlaApi } = await import('@/services/dvlaApi')
    vi.mocked(dvlaApi.get).mockRejectedValueOnce(new Error('offline')).mockResolvedValue(withPlate)
    const store = useDvlaStore()

    await store.fetchStatus('VIN1')
    expect(store.available).toBeNull()
    await store.fetchStatus('VIN1')

    expect(store.available).toBe(true)
  })
})
