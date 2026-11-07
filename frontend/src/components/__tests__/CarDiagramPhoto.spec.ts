import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import CarDiagram from '../CarDiagram.vue'
import { vehiclePhotoApi } from '@/services/vehiclePhotoApi'
import { resetVehiclePhotoCache } from '@/composables/useVehiclePhoto'

vi.mock('@/stores/vehicle', () => ({ useVehicleStore: () => ({ activeVin: 'FAKEVN00000000001' }) }))

vi.mock('@/services/vehiclePhotoApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/vehiclePhotoApi')>()
  return {
    ...actual,
    vehiclePhotoApi: {
      info: vi.fn<typeof actual.vehiclePhotoApi.info>(),
      url: actual.vehiclePhotoApi.url,
      upload: vi.fn<typeof actual.vehiclePhotoApi.upload>(),
      remove: vi.fn<typeof actual.vehiclePhotoApi.remove>(),
    },
  }
})

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

async function mountDiagram(extra: Record<string, unknown> = {}) {
  const wrapper = mount(CarDiagram, {
    props: { frontLeft: 2.4, frontRight: 2.4, rearLeft: 2.4, rearRight: 2.4, ...extra },
    global: { plugins: [i18n], stubs: { FontAwesomeIcon: true } },
  })
  await flushPromises()
  return wrapper
}

describe('CarDiagram with a photo of the car', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    resetVehiclePhotoCache()
    vi.clearAllMocks()
    vi.mocked(vehiclePhotoApi.info).mockResolvedValue({
      hasPhoto: true,
      updatedAt: '2026-11-07T20:00:00Z',
    })
  })

  afterEach(() => vi.unstubAllGlobals())

  it('shows the photo while parked, with what the drawing would mark said in words', async () => {
    const wrapper = await mountDiagram({ isCharging: true, driverDoorOpen: true, evSocPercent: 70 })

    const img = wrapper.find('.car-photo__img')
    expect(img.attributes('src')).toContain('/api/vehicles/FAKEVN00000000001/photo?v=')
    expect(wrapper.find('.tyre-diagram__svg').exists()).toBe(false)
    const chips = wrapper.findAll('.car-photo__chip').map((c) => c.text())
    expect(chips).toEqual(['Charging', en.vehicle.doors_detail.driver])
    expect(wrapper.findAll('.car-photo__tyre')).toHaveLength(4)
  })

  it('draws the car on the road again while it drives', async () => {
    const wrapper = await mountDiagram({ speed: 50 })

    expect(wrapper.find('.car-photo').exists()).toBe(false)
    expect(wrapper.find('.tyre-diagram__svg').exists()).toBe(true)
  })

  it('draws the car when there is no photo, and offers to add one', async () => {
    vi.mocked(vehiclePhotoApi.info).mockResolvedValue({ hasPhoto: false, updatedAt: null })
    const wrapper = await mountDiagram()

    expect(wrapper.find('.car-photo').exists()).toBe(false)
    expect(wrapper.findAll('.car-photo__button')).toHaveLength(1)
    expect(wrapper.find('.car-photo__button').attributes('aria-label')).toBe(en.vehicle.photo.add)
  })

  it('uploads a picked photo and shows it', async () => {
    vi.mocked(vehiclePhotoApi.info).mockResolvedValue({ hasPhoto: false, updatedAt: null })
    vi.mocked(vehiclePhotoApi.upload).mockResolvedValue({
      hasPhoto: true,
      updatedAt: '2026-11-07T21:00:00Z',
    })
    const wrapper = await mountDiagram()
    const file = new File([new Uint8Array([1, 2, 3])], 'car.webp', { type: 'image/webp' })
    const input = wrapper.find('.car-photo__input')
    Object.defineProperty(input.element, 'files', { value: [file] })

    await input.trigger('change')
    await flushPromises()

    expect(vehiclePhotoApi.upload).toHaveBeenCalledWith('FAKEVN00000000001', file)
    expect(wrapper.find('.car-photo__img').attributes('src')).toContain('2026-11-07T21')
  })

  it('removes the photo once confirmed', async () => {
    vi.stubGlobal('confirm', () => true)
    vi.mocked(vehiclePhotoApi.remove).mockResolvedValue(undefined)
    const wrapper = await mountDiagram()

    await wrapper.findAll('.car-photo__button')[1]!.trigger('click')
    await flushPromises()

    expect(vehiclePhotoApi.remove).toHaveBeenCalledWith('FAKEVN00000000001')
    expect(wrapper.find('.car-photo').exists()).toBe(false)
  })
})
