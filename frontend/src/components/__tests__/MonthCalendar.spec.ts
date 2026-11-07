import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import MonthCalendar from '../MonthCalendar.vue'

const i18n = createI18n({ legacy: false, locale: 'en', missingWarn: false, fallbackWarn: false })

describe('MonthCalendar', () => {
  it('shows how many trips or charges each day had, its figure only on hover', () => {
    const wrapper = mount(MonthCalendar, {
      props: {
        month: new Date(2026, 10, 1),
        days: { '2026-11-07': { value: '126 mi', count: 2, level: 1 } },
        selected: null,
        canNext: false,
        countIcon: 'car',
        countLabel: (n: number) => `${n} trips`,
      },
      global: { plugins: [i18n], stubs: { FontAwesomeIcon: true } },
    })

    const day = wrapper.find('.month-cal__day--active')
    expect(day.find('.month-cal__value').exists()).toBe(false)
    expect(day.attributes('title')).toBe('126 mi · 2 trips')
    expect(day.find('.month-cal__count').text()).toBe('2')
    expect(day.attributes('aria-label')).toBe('7: 126 mi, 2 trips')
    expect(wrapper.findAll('.month-cal__count')).toHaveLength(1)
  })
})
