import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, shallowMount } from '@vue/test-utils'
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
    vi.restoreAllMocks()
  })

  // Refresh and Settings live in the sidebar and the phone's top bar, not here as well.
  it('has no Refresh or Settings buttons', () => {
    expect(mountFooter().find('button, .app-footer__btn').exists()).toBe(false)
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
