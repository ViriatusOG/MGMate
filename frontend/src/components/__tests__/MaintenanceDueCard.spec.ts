import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import MaintenanceDueCard from '../MaintenanceDueCard.vue'
import { maintenanceItem } from '@/services/__tests__/maintenanceFixtures'
import type { MaintenanceItem } from '@/services/maintenanceApi'

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

function card(item: MaintenanceItem) {
  return mount(MaintenanceDueCard, {
    props: { item },
    global: { plugins: [i18n, createPinia()], stubs: { FontAwesomeIcon: true } },
  })
}

describe('MaintenanceDueCard', () => {
  it('asks for a start point when the item has nothing to count from', () => {
    const wrapper = card(maintenanceItem({ dueStatus: 'unknown' }))

    expect(wrapper.find('.due-card__badge').text()).toBe(en.maintenance.status.unknown)
    expect(wrapper.find('.due-card__start').text()).toContain(en.maintenance.noStartPoint)
  })

  it('says it counts from registration until a service is logged', () => {
    const wrapper = card(
      maintenanceItem({ countingFrom: 'registration', countingFromDate: '2026-07-31T00:00:00Z' }),
    )

    expect(wrapper.find('.due-card__start').exists()).toBe(false)
    expect(wrapper.text()).toContain('Counting from registration, 31 Jul 2026')
  })

  it('shows the last service once one is logged', () => {
    const wrapper = card(
      maintenanceItem({
        countingFrom: 'service',
        countingFromDate: '2027-07-20T00:00:00Z',
        lastServiceDate: '2027-07-20T00:00:00Z',
      }),
    )

    expect(wrapper.text()).toContain('Last done 20 Jul 2027')
  })
})
