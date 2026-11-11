import { describe, it, expect, vi, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'
import NotificationPanel from '../NotificationPanel.vue'
import type { AppNotification } from '@/services/notificationsApi'
import { ApiError } from '@/services/apiCore'

const i18n = createI18n({ legacy: false, locale: 'en', messages: { en } })

const notification: AppNotification = {
  id: 1,
  title: 'Low tyre pressure',
  body: 'Front left is at 1.9 bar',
  category: 'low-tyre',
  createdAt: '2026-10-15T08:00:00Z',
  isArchived: false,
}

function mountPanel(props: Partial<InstanceType<typeof NotificationPanel>['$props']> = {}) {
  return mount(NotificationPanel, {
    props: { open: true, notifications: [], loading: false, ...props },
    global: { plugins: [i18n], stubs: { FontAwesomeIcon: true, Teleport: true } },
    attachTo: document.body,
  })
}

describe('NotificationPanel', () => {
  it('says the list could not be loaded instead of claiming there is nothing, and offers a retry', async () => {
    const wrapper = mountPanel({ fetchError: new TypeError('Failed to fetch') })

    expect(wrapper.text()).toContain(en.errors.offline)
    expect(wrapper.text()).not.toContain(en.notifications.empty)

    await wrapper
      .findAll('button')
      .find((b) => b.text() === en.common.retry)!
      .trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
    wrapper.unmount()
  })

  it('shows the empty state when the list loaded and is empty', () => {
    const wrapper = mountPanel()

    expect(wrapper.text()).toContain(en.notifications.empty)
    expect(wrapper.find('[role=alert]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('names why an action failed', () => {
    const wrapper = mountPanel({
      notifications: [notification],
      actionError: new ApiError(500, '/api/notifications/1/archive'),
    })

    expect(wrapper.find('[role=alert]').text()).toBe(en.errors.server)
    expect(wrapper.text()).toContain(notification.title)
    wrapper.unmount()
  })

  it('is a modal dialog that takes focus and closes on Escape', () => {
    const wrapper = mountPanel()
    const dialog = wrapper.find('[role=dialog]')

    expect(dialog.attributes('aria-modal')).toBe('true')
    expect(document.activeElement).toBe(dialog.element)

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    expect(wrapper.emitted('close')).toHaveLength(1)
    wrapper.unmount()
  })

  describe('on a notification', () => {
    afterEach(() => {
      vi.useRealTimers()
    })

    const engine: AppNotification = {
      id: 2,
      title: 'Engine started',
      body: 'Your car has been started.',
      category: 'engine-start',
      createdAt: new Date().toISOString(),
      isArchived: false,
    }

    function button(wrapper: ReturnType<typeof mountPanel>, label: string) {
      return wrapper
        .findAll('button')
        .find((b) => b.attributes('aria-label') === label || b.text() === label)!
    }

    it('opens the page the notification is about when tapped', async () => {
      const wrapper = mountPanel({ notifications: [engine] })

      await wrapper.find('.notif-item__main').trigger('click')

      expect(wrapper.emitted('navigate')).toEqual([['/trip-log']])
      wrapper.unmount()
    })

    it('groups the list by day', () => {
      const wrapper = mountPanel({ notifications: [engine, notification] })

      const days = wrapper.findAll('.notif-group__day').map((d) => d.text())
      expect(days[0]).toBe(en.notifications.today)
      expect(days).toHaveLength(2)
      wrapper.unmount()
    })

    it('archives after the Undo bar has gone, and not at all after Undo', async () => {
      vi.useFakeTimers()
      const wrapper = mountPanel({ notifications: [engine] })

      await button(wrapper, en.notifications.archive).trigger('click')
      expect(wrapper.text()).not.toContain('Your car has been started.')
      expect(wrapper.find('[role=status]').text()).toContain(en.notifications.undo)
      expect(wrapper.emitted('archive')).toBeUndefined()

      await wrapper.find('.notif-panel__undo-btn').trigger('click')
      vi.advanceTimersByTime(6000)
      expect(wrapper.emitted('archive')).toBeUndefined()
      expect(wrapper.text()).toContain('Your car has been started.')

      await button(wrapper, en.notifications.archive).trigger('click')
      vi.advanceTimersByTime(6000)
      expect(wrapper.emitted('archive')).toEqual([[2]])
      wrapper.unmount()
    })

    it('sends a waiting delete straight away when the panel closes', async () => {
      const wrapper = mountPanel({ notifications: [engine] })

      await button(wrapper, en.notifications.delete).trigger('click')
      await wrapper.setProps({ open: false })

      expect(wrapper.emitted('delete')).toEqual([[2]])
      wrapper.unmount()
    })

    it('asks before deleting everything', async () => {
      const wrapper = mountPanel({ notifications: [engine, notification] })

      await button(wrapper, en.notifications.deleteAll).trigger('click')
      const sheet = wrapper.find('[role=alertdialog]')
      expect(sheet.text()).toContain('Delete all 2 notifications?')
      expect(wrapper.emitted('deleteAll')).toBeUndefined()

      await sheet
        .findAll('button')
        .find((b) => b.text().includes(en.notifications.deleteAll))!
        .trigger('click')
      expect(wrapper.emitted('deleteAll')).toHaveLength(1)
      expect(wrapper.find('[role=alertdialog]').exists()).toBe(false)
      wrapper.unmount()
    })

    it('closes the question, not the panel, on Escape', async () => {
      const wrapper = mountPanel({ notifications: [engine] })

      await button(wrapper, en.notifications.archiveAll).trigger('click')
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
      await wrapper.vm.$nextTick()

      expect(wrapper.find('[role=alertdialog]').exists()).toBe(false)
      expect(wrapper.emitted('close')).toBeUndefined()
      wrapper.unmount()
    })
  })
})
