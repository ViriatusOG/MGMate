import { describe, it, expect } from 'vitest'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import { formatTimeSpan } from '@/utils/timeSpan'

const { t } = createI18n({ legacy: false, locale: 'en', messages: { en } }).global
const day = (y: number, m: number, d: number) => new Date(Date.UTC(y, m - 1, d))
const today = day(2026, 11, 9)

describe('formatTimeSpan', () => {
  it.each([
    [day(2026, 11, 10), '1 day'],
    [day(2026, 12, 1), '22 days'],
    [day(2027, 1, 31), '2 mo 22 d'],
    [day(2027, 7, 31), '8 mo 22 d'],
    [day(2027, 7, 9), '8 mo'],
    [day(2029, 7, 31), '2 yr 8 mo'],
    [day(2033, 11, 9), '7 years'],
  ])('counts to %s as %s', (to, text) => {
    expect(formatTimeSpan(t, today, to)).toBe(text)
  })
})
