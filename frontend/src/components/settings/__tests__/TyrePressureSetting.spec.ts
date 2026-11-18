import { beforeEach, describe, it, expect, vi } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import { useUiSettingsStore } from '@/stores/settingsUi'
import TyrePressureSetting from '../TyrePressureSetting.vue'

vi.mock('@/services/vehicleApi', () => ({
  vehicleApi: { tyrePressureThresholds: () => Promise.reject(new Error('offline')) },
}))

function mountSetting() {
  const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })
  return mount(TyrePressureSetting, { global: { plugins: [i18n] } })
}

function inputs(wrapper: ReturnType<typeof mountSetting>) {
  return wrapper.findAll('input')
}

describe('TyrePressureSetting', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    const ui = useUiSettingsStore()
    ui.units = { ...ui.units, pressure: 'psi' }
  })

  it("starts every tyre at MG's pressure, in the chosen unit", () => {
    const wrapper = mountSetting()

    expect(inputs(wrapper).map((i) => (i.element as HTMLInputElement).value)).toEqual([
      '36',
      '36',
      '36',
      '36',
    ])
    expect(wrapper.text()).toContain("Use MG's 36 psi")
  })

  it('saves summer and winter in bar, keeping the season', async () => {
    const wrapper = mountSetting()
    const [, , winterFront, winterRear] = inputs(wrapper)
    await winterFront!.setValue('38')
    await winterRear!.setValue('39')

    await wrapper.find('form').trigger('submit')

    const own = useUiSettingsStore().tyrePressures!
    expect(own.season).toBe('summer')
    expect(own.winter.frontBar).toBeCloseTo(38 / 14.5038, 3)
    expect(own.winter.rearBar).toBeCloseTo(39 / 14.5038, 3)
    expect(own.summer.frontBar).toBeCloseTo(2.5, 2)
  })

  it('switches season by hand and marks the pair in use', async () => {
    const wrapper = mountSetting()

    await wrapper.find('select').setValue('winter')
    await nextTick()

    expect(useUiSettingsStore().tyrePressures?.season).toBe('winter')
    expect(wrapper.find('.badge').element.parentElement!.textContent).toContain('Winter')
  })

  it('refuses a pressure no tyre is set to', async () => {
    const wrapper = mountSetting()

    await inputs(wrapper)[0]!.setValue('360')

    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeDefined()
  })

  it("goes back to MG's pressure", async () => {
    useUiSettingsStore().tyrePressures = {
      season: 'winter',
      summer: { frontBar: 2.5, rearBar: 2.5 },
      winter: { frontBar: 2.7, rearBar: 2.7 },
    }
    const wrapper = mountSetting()

    await wrapper
      .findAll('button')
      .find((b) => b.text().startsWith('Use MG'))!
      .trigger('click')

    expect(useUiSettingsStore().tyrePressures).toBeNull()
  })
})
