import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { flushPromises, mount, RouterLinkStub, shallowMount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import AppFooter from '../AppFooter.vue'

const appVersion = vi.hoisted(() => ({ value: null as string | null }))

const update = vi.hoisted(() => ({
  fails: false,
  answer: { updateAvailable: false, latestVersion: null, releaseUrl: null } as {
    updateAvailable: boolean
    latestVersion: string | null
    releaseUrl: string | null
  },
}))

vi.mock('@/services/updatesApi', () => ({
  updatesApi: {
    get: () =>
      update.fails ? Promise.reject(new Error('offline')) : Promise.resolve(update.answer),
  },
}))

vi.mock('@/utils/appVersion', () => ({
  get APP_VERSION() {
    return appVersion.value
  },
}))

const vehicle = vi.hoisted(() => ({ activeVin: null as string | null }))
const sendCommand = vi.hoisted(() => vi.fn<(vin: string, command: string, value: string) => void>())

vi.mock('@/stores/vehicle', () => ({
  useVehicleStore: () => ({ loading: false, activeVin: vehicle.activeVin }),
}))

vi.mock('@/composables/useVehicleCommand', () => ({
  useVehicleCommand: () => ({ sending: ref(null), send: sendCommand }),
}))

const i18n = createI18n({ legacy: false, locale: 'en', missingWarn: false, fallbackWarn: false })

function mountFooter() {
  return shallowMount(AppFooter, {
    global: { plugins: [i18n], stubs: { FontAwesomeIcon: true } },
  })
}

describe('AppFooter', () => {
  beforeEach(() => {
    appVersion.value = null
    update.fails = false
    update.answer = { updateAvailable: false, latestVersion: null, releaseUrl: null }
    vehicle.activeVin = null
    sendCommand.mockClear()
    vi.restoreAllMocks()
  })

  // Waking the car costs 12V battery, so the forced refresh asks first.
  it('wakes the car only after the owner confirms', async () => {
    vehicle.activeVin = 'VIN1'
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mountFooter()
    const button = wrapper.find('button[aria-label="control.refresh"]')

    await button.trigger('click')
    expect(confirm).toHaveBeenCalledWith('control.refreshConfirm')
    expect(sendCommand).not.toHaveBeenCalled()

    confirm.mockReturnValue(true)
    await button.trigger('click')
    expect(sendCommand).toHaveBeenCalledWith('VIN1', 'refresh', 'force')
  })

  it('links to the settings page', () => {
    const wrapper = mount(AppFooter, {
      global: { plugins: [i18n], stubs: { FontAwesomeIcon: true, RouterLink: RouterLinkStub } },
    })
    expect(wrapper.findComponent(RouterLinkStub).props('to')).toBe('/settings')
  })

  it('separates the project name and version with a space', () => {
    appVersion.value = 'v0.5.0'
    expect(mountFooter().find('.app-footer__copyright').text()).toBe('© 2026 MGMate v0.5.0')
  })

  it('shows only the project name when no version was injected', () => {
    expect(mountFooter().find('.app-footer__copyright').text()).toBe('© 2026 MGMate')
  })

  it('links to a newer release when the API found one', async () => {
    update.answer = {
      updateAvailable: true,
      latestVersion: '0.9.3',
      releaseUrl: 'https://github.com/ViriatusOG/MGMate/releases/tag/v0.9.3',
    }
    const wrapper = mountFooter()
    await flushPromises()

    const link = wrapper.find('a.app-footer__update')
    expect(link.attributes('href')).toBe('https://github.com/ViriatusOG/MGMate/releases/tag/v0.9.3')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('shows no update link while the running version is the latest', async () => {
    const wrapper = mountFooter()
    await flushPromises()

    expect(wrapper.find('a.app-footer__update').exists()).toBe(false)
  })

  // The API builds the link itself; a link anywhere else is not followed regardless.
  it('ignores a release link that does not point at GitHub', async () => {
    update.answer = {
      updateAvailable: true,
      latestVersion: '0.9.3',
      releaseUrl: 'https://example.com/v0.9.3',
    }
    const wrapper = mountFooter()
    await flushPromises()

    expect(wrapper.find('a.app-footer__update').exists()).toBe(false)
  })

  it('shows nothing when the API cannot be asked', async () => {
    update.fails = true
    const wrapper = mountFooter()
    await flushPromises()

    expect(wrapper.find('a.app-footer__update').exists()).toBe(false)
  })
})
