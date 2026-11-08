import { describe, expect, it } from 'vitest'
import { dateInputValue, defaultSearch, matchesText, searchRange } from '@/utils/logSearch'

describe('searchRange', () => {
  it('covers the whole of the last day', () => {
    const range = searchRange({ from: '2026-10-01', to: '2026-10-31' })
    expect(range).toEqual({ ok: true, from: new Date(2026, 9, 1), to: new Date(2026, 10, 1) })
  })

  it('turns down a range it cannot search', () => {
    expect(searchRange({ from: '', to: '2026-10-31' })).toEqual({ ok: false, reason: 'missing' })
    expect(searchRange({ from: '2026-10-31', to: '2026-10-01' })).toEqual({
      ok: false,
      reason: 'backwards',
    })
    expect(searchRange({ from: '2025-01-01', to: '2026-01-02' })).toEqual({
      ok: false,
      reason: 'tooLong',
    })
  })

  it('starts on the year up to today', () => {
    const query = defaultSearch(new Date(2026, 10, 7))
    expect(query).toEqual({ text: '', from: '2025-11-08', to: '2026-11-07' })
    expect(searchRange(query).ok).toBe(true)
    expect(dateInputValue(new Date(2026, 0, 5))).toBe('2026-01-05')
  })
})

describe('matchesText', () => {
  it('needs every word, in any of the texts, whatever the case or accents', () => {
    const texts = ['Rua de São Bento, Lisboa', null, 'Visit the dentist']
    expect(matchesText(texts, 'sao lisboa')).toBe(true)
    expect(matchesText(texts, 'DENTIST bento')).toBe(true)
    expect(matchesText(texts, 'porto')).toBe(false)
    expect(matchesText(texts, '   ')).toBe(true)
  })
})
