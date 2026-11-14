import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import LicensePlateBadge from '../LicensePlateBadge.vue'

describe('LicensePlateBadge', () => {
  it('shows a British electric car its plate with the green flash', () => {
    const badge = mount(LicensePlateBadge, {
      props: { plate: 'AB26CDE', plateStyle: 'gb', electric: true },
    })

    expect(badge.text()).toBe('AB26 CDE')
    expect(badge.classes()).toContain('plate-badge--flash')
  })

  it('leaves the flash off a British car that burns fuel, and off any other country', () => {
    const hybrid = mount(LicensePlateBadge, { props: { plate: 'AB26CDE', plateStyle: 'gb' } })
    const dutch = mount(LicensePlateBadge, {
      props: { plate: 'AB123C', plateStyle: 'nl', electric: true },
    })

    expect(hybrid.classes()).not.toContain('plate-badge--flash')
    expect(dutch.text()).toBe('AB-123-C')
    expect(dutch.classes()).not.toContain('plate-badge--flash')
  })
})
