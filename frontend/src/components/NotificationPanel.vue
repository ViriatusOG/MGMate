<script setup lang="ts">
import '@/assets/notifications.css'
import { ref, computed, watch, nextTick, onBeforeUnmount } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppNotification } from '@/services/notificationsApi'
import { useInfiniteScroll } from '@/composables/useInfiniteScroll'
import { useRelativeTime } from '@/composables/useRelativeTime'
import { useErrorMessage } from '@/composables/useErrorMessage'
import { useEscapeKey } from '@/composables/useEscapeKey'
import { useDialogFocus } from '@/composables/useDialogFocus'
import { notificationPage } from '@/utils/notificationCategories'
import { formatDate, formatTime } from '@/utils/format'

const PAGE_SIZE = 10
/** How long the Undo bar stays after archiving or deleting one notification. */
const UNDO_MS = 5000
/** Width of one action revealed by a swipe, in px; matches .notif-item__swipe-btn. */
const SWIPE_ACTION_WIDTH = 84
/** A swipe past this share of the row's width archives (or, in Archived, deletes) at once. */
const FULL_SWIPE_SHARE = 0.6

const CATEGORY_ICONS: Record<string, string[]> = {
  'low-tyre': ['fas', 'circle-exclamation'],
  'high-tyre': ['fas', 'circle-exclamation'],
  'slow-puncture': ['fas', 'arrow-trend-down'],
  'low-ev': ['fas', 'battery-quarter'],
  'charging-complete': ['fas', 'plug-circle-check'],
  'charging-interrupted': ['fas', 'plug-circle-exclamation'],
  'engine-start': ['fas', 'car'],
  'unlocked-parked': ['fas', 'lock-open'],
  'doors-open-parked': ['fas', 'door-open'],
  'windows-open-parked': ['fas', 'wind'],
  'moved-parked': ['fas', 'truck-pickup'],
  'vehicle-message': ['fas', 'message'],
  'climate-schedule': ['fas', 'calendar-days'],
  'low-12v': ['fas', 'car-battery'],
  'low-fuel': ['fas', 'gas-pump'],
  'gateway-login': ['fas', 'triangle-exclamation'],
  'data-stale': ['fas', 'clock'],
  'place-arrival': ['fas', 'house'],
  'place-departure': ['fas', 'arrow-right-from-bracket'],
}

// The tint of the icon tile: green for good news, amber for a warning, red for a problem.
const CATEGORY_TONES: Record<string, 'good' | 'warn' | 'bad'> = {
  'charging-complete': 'good',
  'place-arrival': 'good',
  'low-tyre': 'warn',
  'high-tyre': 'warn',
  'slow-puncture': 'warn',
  'low-ev': 'warn',
  'low-12v': 'warn',
  'low-fuel': 'warn',
  'data-stale': 'warn',
  'doors-open-parked': 'warn',
  'windows-open-parked': 'warn',
  'charging-interrupted': 'bad',
  'moved-parked': 'bad',
  'gateway-login': 'bad',
}

function categoryIcon(category: string | null): string[] {
  if (category?.startsWith('maintenance-')) return ['fas', 'screwdriver-wrench']
  if (category?.startsWith('rdw-')) return ['fas', 'clipboard-check']
  return (category && CATEGORY_ICONS[category]) || ['fas', 'bell']
}

function categoryTone(category: string | null): string {
  if (category?.startsWith('rdw-recall')) return 'notif-item__icon--bad'
  const tone = category ? CATEGORY_TONES[category] : undefined
  return tone ? `notif-item__icon--${tone}` : ''
}

const props = defineProps<{
  open: boolean
  notifications: AppNotification[]
  loading: boolean
  /** Why the list could not be loaded; the panel then offers to try again. */
  fetchError?: Error | null
  actionError?: Error | null
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'archive', id: number): void
  (e: 'delete', id: number): void
  (e: 'archiveAll'): void
  (e: 'deleteAll'): void
  (e: 'retry'): void
  /** Open a page of the app (the one a notification is about, or Settings); the panel closes. */
  (e: 'navigate', path: string): void
}>()

const { t } = useI18n()
const errorMessage = useErrorMessage()

const showArchived = ref(false)
const pendingBulk = ref<'archiveAll' | 'deleteAll' | null>(null)
const bodyRef = ref<HTMLElement | null>(null)

// ── Undo ──────────────────────────────────────────────────────────
// Archiving or deleting one notification hides it at once but only tells the server after the
// Undo bar has gone, so Undo needs no way back on the server. Anything that moves on (another
// action, closing the panel, leaving the page) sends a waiting one straight away.

interface PendingAction {
  id: number
  kind: 'archive' | 'delete'
  title: string
}

const pending = ref<PendingAction | null>(null)
let pendingTimer: ReturnType<typeof setTimeout> | null = null

function clearPendingTimer() {
  if (pendingTimer !== null) clearTimeout(pendingTimer)
  pendingTimer = null
}

function commitPending() {
  clearPendingTimer()
  const action = pending.value
  if (!action) return
  pending.value = null
  if (action.kind === 'archive') emit('archive', action.id)
  else emit('delete', action.id)
}

function startAction(n: AppNotification, kind: 'archive' | 'delete') {
  commitPending()
  revealedId.value = null
  pending.value = { id: n.id, kind, title: n.title }
  pendingTimer = setTimeout(commitPending, UNDO_MS)
}

function undo() {
  clearPendingTimer()
  pending.value = null
}

function onPageHide() {
  commitPending()
}
window.addEventListener('pagehide', onPageHide)
onBeforeUnmount(() => {
  window.removeEventListener('pagehide', onPageHide)
  commitPending()
})

// ── Lists ─────────────────────────────────────────────────────────

const visible = computed(() => props.notifications.filter((n) => n.id !== pending.value?.id))
const activeCount = computed(() => visible.value.filter((n) => !n.isArchived).length)
const hasUnarchived = computed(() => activeCount.value > 0)

const filteredNotifications = computed(() =>
  visible.value.filter((n) => n.isArchived === showArchived.value),
)

const {
  displayItems: pagedNotifications,
  sentinelRef,
  reset,
  observe,
  disconnect,
} = useInfiniteScroll(filteredNotifications, PAGE_SIZE)

const { relativeTime } = useRelativeTime()

function startOfDay(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
}

function dayLabel(d: Date): string {
  const now = new Date()
  const day = startOfDay(d)
  if (day === startOfDay(now)) return t('notifications.today')
  const yesterday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1)
  if (day === yesterday.getTime()) return t('notifications.yesterday')
  const sameYear = d.getFullYear() === now.getFullYear()
  return formatDate(d, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    ...(sameYear ? {} : { year: 'numeric' }),
  })
}

/** "13 min ago" within the hour, otherwise the time of day (the day is the group heading). */
function itemTime(createdAt: string): string {
  const d = new Date(createdAt)
  return Date.now() - d.getTime() < 3_600_000 ? relativeTime(d) : formatTime(d)
}

// The list a page at a time, cut into days. The list comes newest first, so a day's
// notifications are always together.
const groups = computed(() => {
  const result: { key: string; label: string; items: AppNotification[] }[] = []
  for (const n of pagedNotifications.value) {
    const d = new Date(n.createdAt)
    const key = String(startOfDay(d))
    const last = result[result.length - 1]
    if (last && last.key === key) last.items.push(n)
    else result.push({ key, label: dayLabel(d), items: [n] })
  }
  return result
})

watch(
  () => props.notifications.length,
  () => {
    reset()
  },
)
watch(
  () => props.loading,
  async (isLoading) => {
    if (!isLoading && props.open) {
      await nextTick()
      observe(bodyRef.value)
    }
  },
)
watch(
  () => props.open,
  async (v) => {
    if (v) {
      reset()
      showArchived.value = false
      pendingBulk.value = null
      revealedId.value = null
      await nextTick()
      observe(bodyRef.value)
    } else {
      commitPending()
      disconnect()
    }
  },
)
watch(showArchived, async () => {
  revealedId.value = null
  reset()
  await nextTick()
  observe(bodyRef.value)
})

// ── Opening a notification ────────────────────────────────────────

function pageOf(n: AppNotification): string | null {
  return notificationPage(n.category)
}

function openNotification(n: AppNotification) {
  if (swipedJustNow) return
  // A tap on a row that's swiped open closes it again, as on a phone's own lists.
  if (revealedId.value !== null) {
    revealedId.value = null
    return
  }
  const page = pageOf(n)
  if (page) emit('navigate', page)
}

// ── Swiping a row ─────────────────────────────────────────────────
// Touch and pen only: a mouse has the buttons that show on hover. A drag left reveals Archive
// and Delete behind the row (only Delete in Archived); a long drag does the first at once.

const revealedId = ref<number | null>(null)
const drag = ref<{ id: number; dx: number } | null>(null)
let start: {
  id: number
  x: number
  y: number
  base: number
  width: number
  pointer: number
} | null = null
let axis: 'x' | 'y' | null = null
let swipedJustNow = false

function actionsWidth(): number {
  return (showArchived.value ? 1 : 2) * SWIPE_ACTION_WIDTH
}

function offsetOf(id: number): number {
  if (drag.value?.id === id) return drag.value.dx
  return revealedId.value === id ? -actionsWidth() : 0
}

function onPointerDown(e: PointerEvent, n: AppNotification) {
  if (e.pointerType === 'mouse' || !e.isPrimary) return
  if (revealedId.value !== null && revealedId.value !== n.id) revealedId.value = null
  const row = e.currentTarget as HTMLElement
  start = {
    id: n.id,
    x: e.clientX,
    y: e.clientY,
    base: revealedId.value === n.id ? -actionsWidth() : 0,
    width: row.offsetWidth || 360,
    pointer: e.pointerId,
  }
  axis = null
}

function onPointerMove(e: PointerEvent) {
  if (!start || e.pointerId !== start.pointer) return
  const dx = e.clientX - start.x
  const dy = e.clientY - start.y
  if (axis === null) {
    if (Math.abs(dx) < 8 && Math.abs(dy) < 8) return
    axis = Math.abs(dx) > Math.abs(dy) ? 'x' : 'y'
    if (axis === 'x') (e.currentTarget as HTMLElement).setPointerCapture?.(e.pointerId)
  }
  if (axis !== 'x') return
  e.preventDefault()
  drag.value = { id: start.id, dx: Math.min(0, Math.max(-start.width, start.base + dx)) }
}

function onPointerUp(n: AppNotification) {
  if (!start) return
  const wasSwipe = axis === 'x'
  const dx = drag.value?.dx ?? 0
  const width = start.width
  start = null
  axis = null
  drag.value = null
  if (!wasSwipe) return
  swipedJustNow = true
  setTimeout(() => (swipedJustNow = false), 0)
  if (dx < -width * FULL_SWIPE_SHARE) startAction(n, showArchived.value ? 'delete' : 'archive')
  else revealedId.value = dx < -actionsWidth() / 2 ? n.id : null
}

function onPointerCancel() {
  start = null
  axis = null
  drag.value = null
}

// ── Archive all / Delete all ──────────────────────────────────────

function requestBulkAction(action: 'archiveAll' | 'deleteAll') {
  commitPending()
  revealedId.value = null
  pendingBulk.value = action
}

function confirmBulkAction() {
  if (pendingBulk.value === 'archiveAll') emit('archiveAll')
  else if (pendingBulk.value === 'deleteAll') emit('deleteAll')
  pendingBulk.value = null
}

function cancelBulkAction() {
  pendingBulk.value = null
}

const confirmCancelRef = ref<HTMLElement | null>(null)
watch(pendingBulk, async (action) => {
  if (!action) return
  await nextTick()
  confirmCancelRef.value?.focus()
})

useEscapeKey(
  () => props.open,
  () => {
    if (pendingBulk.value) cancelBulkAction()
    else emit('close')
  },
)

const panel = ref<HTMLElement | null>(null)
useDialogFocus(panel, () => props.open)
</script>

<template>
  <Teleport to="body">
    <Transition name="notif-panel">
      <div v-if="open" class="notif-backdrop" @click.self="emit('close')">
        <div
          ref="panel"
          class="notif-panel"
          tabindex="-1"
          role="dialog"
          :aria-modal="true"
          :aria-label="t('notifications.title')"
        >
          <div class="notif-panel__header">
            <h3 class="notif-panel__title">{{ t('notifications.title') }}</h3>
            <button
              class="notif-panel__icon-btn"
              :title="t('notifications.settings')"
              :aria-label="t('notifications.settings')"
              @click="emit('navigate', '/settings#notifications')"
            >
              <font-awesome-icon icon="sliders" aria-hidden="true" />
            </button>
            <button
              class="notif-panel__icon-btn"
              :aria-label="t('common.close')"
              @click="emit('close')"
            >
              <font-awesome-icon icon="xmark" aria-hidden="true" />
            </button>
          </div>

          <div v-if="actionError" class="notif-panel__error text-danger" role="alert">
            <font-awesome-icon icon="triangle-exclamation" />
            {{ errorMessage(actionError) }}
          </div>

          <div v-if="fetchError && !loading" class="notif-panel__error text-danger" role="alert">
            <font-awesome-icon icon="triangle-exclamation" />
            <span>{{ errorMessage(fetchError) }}</span>
            <button class="btn btn-sm btn-outline-secondary" @click="emit('retry')">
              {{ t('common.retry') }}
            </button>
          </div>

          <div v-if="!loading && notifications.length > 0" class="notif-panel__tabs" role="tablist">
            <button
              class="notif-panel__tab"
              role="tab"
              :aria-selected="!showArchived"
              :class="{ 'notif-panel__tab--active': !showArchived }"
              @click="showArchived = false"
            >
              {{ t('notifications.active') }}
              <span v-if="activeCount > 0" class="notif-panel__count">{{ activeCount }}</span>
            </button>
            <button
              class="notif-panel__tab"
              role="tab"
              :aria-selected="showArchived"
              :class="{ 'notif-panel__tab--active': showArchived }"
              @click="showArchived = true"
            >
              <font-awesome-icon icon="box-archive" />
              {{ t('notifications.archived') }}
            </button>
          </div>

          <div ref="bodyRef" class="notif-panel__body">
            <div v-if="loading" class="notif-panel__empty text-muted">
              <font-awesome-icon icon="spinner" spin />
              {{ t('common.loading') }}
            </div>

            <div
              v-else-if="filteredNotifications.length === 0 && !fetchError"
              class="notif-panel__empty text-muted"
            >
              <font-awesome-icon icon="bell-slash" class="notif-panel__empty-icon" />
              <span>{{
                showArchived ? t('notifications.emptyArchived') : t('notifications.empty')
              }}</span>
            </div>

            <template v-else-if="filteredNotifications.length > 0">
              <p class="notif-panel__hint">
                <font-awesome-icon icon="hand-pointer" aria-hidden="true" />
                {{
                  showArchived ? t('notifications.swipeHintArchived') : t('notifications.swipeHint')
                }}
              </p>
              <section v-for="g in groups" :key="g.key" class="notif-group">
                <h4 class="notif-group__day">{{ g.label }}</h4>
                <ul class="notif-panel__list">
                  <li
                    v-for="n in g.items"
                    :key="n.id"
                    class="notif-item"
                    :class="{
                      'notif-item--archived': n.isArchived,
                      'notif-item--dragging': drag?.id === n.id,
                    }"
                  >
                    <div class="notif-item__swipe" aria-hidden="true">
                      <button
                        v-if="!n.isArchived"
                        class="notif-item__swipe-btn notif-item__swipe-btn--archive"
                        tabindex="-1"
                        @click="startAction(n, 'archive')"
                      >
                        <font-awesome-icon icon="box-archive" />
                        {{ t('notifications.archive') }}
                      </button>
                      <button
                        class="notif-item__swipe-btn notif-item__swipe-btn--delete"
                        tabindex="-1"
                        @click="startAction(n, 'delete')"
                      >
                        <font-awesome-icon icon="trash" />
                        {{ t('notifications.delete') }}
                      </button>
                    </div>

                    <div
                      class="notif-item__face"
                      :class="{ 'notif-item__face--link': pageOf(n) }"
                      :style="
                        offsetOf(n.id)
                          ? { transform: `translateX(${offsetOf(n.id)}px)` }
                          : undefined
                      "
                      @pointerdown="onPointerDown($event, n)"
                      @pointermove="onPointerMove"
                      @pointerup="onPointerUp(n)"
                      @pointercancel="onPointerCancel"
                    >
                      <button
                        class="notif-item__main"
                        :aria-disabled="!pageOf(n) || undefined"
                        @click="openNotification(n)"
                      >
                        <span class="notif-item__icon" :class="categoryTone(n.category)">
                          <font-awesome-icon :icon="categoryIcon(n.category)" />
                        </span>
                        <span class="notif-item__content">
                          <span class="notif-item__head">
                            <span class="notif-item__title">{{ n.title }}</span>
                            <span class="notif-item__time">{{ itemTime(n.createdAt) }}</span>
                          </span>
                          <span class="notif-item__body">{{ n.body }}</span>
                        </span>
                        <font-awesome-icon
                          v-if="pageOf(n)"
                          icon="chevron-right"
                          class="notif-item__chevron"
                          aria-hidden="true"
                        />
                      </button>
                      <div class="notif-item__actions">
                        <button
                          v-if="!n.isArchived"
                          class="notif-item__btn"
                          :title="t('notifications.archive')"
                          :aria-label="t('notifications.archive')"
                          @click="startAction(n, 'archive')"
                        >
                          <font-awesome-icon icon="box-archive" aria-hidden="true" />
                        </button>
                        <button
                          class="notif-item__btn notif-item__btn--danger"
                          :title="t('notifications.delete')"
                          :aria-label="t('notifications.delete')"
                          @click="startAction(n, 'delete')"
                        >
                          <font-awesome-icon icon="trash" aria-hidden="true" />
                        </button>
                      </div>
                    </div>
                  </li>
                </ul>
              </section>
              <div ref="sentinelRef" aria-hidden="true" />
            </template>
          </div>

          <div v-if="pending" class="notif-panel__undo" role="status">
            <font-awesome-icon
              :icon="pending.kind === 'archive' ? 'box-archive' : 'trash'"
              class="text-muted"
            />
            <span class="notif-panel__undo-msg">{{
              pending.kind === 'archive'
                ? t('notifications.archivedOne', { title: pending.title })
                : t('notifications.deletedOne', { title: pending.title })
            }}</span>
            <button class="notif-panel__undo-btn" @click="undo">
              {{ t('notifications.undo') }}
            </button>
          </div>

          <div v-if="!loading && notifications.length > 0" class="notif-panel__footer">
            <button
              v-if="!showArchived && hasUnarchived"
              class="notif-panel__bulk-btn"
              @click="requestBulkAction('archiveAll')"
            >
              <font-awesome-icon icon="box-archive" />
              {{ t('notifications.archiveAll') }}
            </button>
            <button
              class="notif-panel__bulk-btn notif-panel__bulk-btn--danger"
              @click="requestBulkAction('deleteAll')"
            >
              <font-awesome-icon icon="trash" />
              {{ t('notifications.deleteAll') }}
            </button>
          </div>

          <Transition name="notif-confirm">
            <div v-if="pendingBulk" class="notif-confirm" @click.self="cancelBulkAction">
              <div
                class="notif-confirm__sheet"
                role="alertdialog"
                aria-labelledby="notif-confirm-title"
                aria-describedby="notif-confirm-hint"
              >
                <h4 id="notif-confirm-title" class="notif-confirm__title">
                  {{
                    pendingBulk === 'archiveAll'
                      ? t('notifications.confirmArchiveAll', { n: activeCount })
                      : t('notifications.confirmDeleteAll', { n: visible.length })
                  }}
                </h4>
                <p id="notif-confirm-hint" class="notif-confirm__hint">
                  {{
                    pendingBulk === 'archiveAll'
                      ? t('notifications.confirmArchiveAllHint')
                      : t('notifications.confirmDeleteAllHint')
                  }}
                </p>
                <div class="notif-confirm__actions">
                  <button
                    ref="confirmCancelRef"
                    class="notif-panel__bulk-btn"
                    @click="cancelBulkAction"
                  >
                    {{ t('common.cancel') }}
                  </button>
                  <button
                    class="notif-panel__bulk-btn"
                    :class="
                      pendingBulk === 'deleteAll'
                        ? 'notif-panel__bulk-btn--confirm-danger'
                        : 'notif-panel__bulk-btn--confirm'
                    "
                    @click="confirmBulkAction"
                  >
                    <font-awesome-icon
                      :icon="pendingBulk === 'deleteAll' ? 'trash' : 'box-archive'"
                    />
                    {{
                      pendingBulk === 'deleteAll'
                        ? t('notifications.deleteAll')
                        : t('notifications.archiveAll')
                    }}
                  </button>
                </div>
              </div>
            </div>
          </Transition>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
