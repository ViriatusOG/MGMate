import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, shallowMount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import AppVersion from '../AppVersion.vue'

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

const i18n = createI18n({ legacy: false, locale: 'en', missingWarn: false, fallbackWarn: false })

function mountVersion() {
  return shallowMount(AppVersion, {
    global: { plugins: [i18n], stubs: { FontAwesomeIcon: true } },
  })
}

describe('AppVersion', () => {
  beforeEach(() => {
    appVersion.value = null
    update.fails = false
    update.answer = { updateAvailable: false, latestVersion: null, releaseUrl: null }
    vi.restoreAllMocks()
  })

  it('separates the project name and version with a space', () => {
    appVersion.value = 'v0.5.0'
    expect(mountVersion().find('.app-version__copyright').text()).toBe('© 2026 MGMate v0.5.0')
  })

  it('shows only the project name when no version was injected', () => {
    expect(mountVersion().find('.app-version__copyright').text()).toBe('© 2026 MGMate')
  })

  it('links to a newer release when the API found one', async () => {
    update.answer = {
      updateAvailable: true,
      latestVersion: '0.9.3',
      releaseUrl: 'https://github.com/ViriatusOG/MGMate/releases/tag/v0.9.3',
    }
    const wrapper = mountVersion()
    await flushPromises()

    const link = wrapper.find('a.app-version__update')
    expect(link.attributes('href')).toBe('https://github.com/ViriatusOG/MGMate/releases/tag/v0.9.3')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('shows no update link while the running version is the latest', async () => {
    const wrapper = mountVersion()
    await flushPromises()

    expect(wrapper.find('a.app-version__update').exists()).toBe(false)
  })

  // The API builds the link itself; a link anywhere else is not followed regardless.
  it('ignores a release link that does not point at GitHub', async () => {
    update.answer = {
      updateAvailable: true,
      latestVersion: '0.9.3',
      releaseUrl: 'https://example.com/v0.9.3',
    }
    const wrapper = mountVersion()
    await flushPromises()

    expect(wrapper.find('a.app-version__update').exists()).toBe(false)
  })

  it('shows nothing when the API cannot be asked', async () => {
    update.fails = true
    const wrapper = mountVersion()
    await flushPromises()

    expect(wrapper.find('a.app-version__update').exists()).toBe(false)
  })
})
