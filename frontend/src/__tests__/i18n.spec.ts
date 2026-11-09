import { describe, it, expect, afterEach } from 'vitest'
import { i18n, setLocale } from '@/i18n'

describe('i18n', () => {
  afterEach(() => {
    i18n.global.locale.value = 'en'
  })

  it('names the interface language on the page', () => {
    expect(document.documentElement.lang).toBe('en')
  })

  it('ships English only', () => {
    expect(i18n.global.availableLocales).toEqual(['en'])
    expect(i18n.global.t('notifications.title')).toBe('Notifications')
  })

  it('stays in English when set to English', async () => {
    await setLocale('en')

    expect(i18n.global.locale.value).toBe('en')
  })
})
