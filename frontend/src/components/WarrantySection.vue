<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useWarrantyStore } from '@/stores/warranty'
import type { WarrantyCover } from '@/services/warrantyApi'
import { useUnits } from '@/composables/useUnits'
import { useStoreAction } from '@/composables/useStoreAction'
import { useErrorMessage } from '@/composables/useErrorMessage'
import { ODOMETER_FORMAT } from '@/utils/units'
import { formatDate } from '@/utils/format'
import { COVER_DATE, sortCovers, todayUtc } from '@/utils/warranty'
import { formatTimeSpan, utcDay } from '@/utils/timeSpan'
import WarrantyEditModal from './WarrantyEditModal.vue'

const props = defineProps<{ vin: string }>()

const { t } = useI18n()
const store = useWarrantyStore()
const units = useUnits()
const errorMessage = useErrorMessage()

// How many rows the phone list shows before "Show all".
const PHONE_ROWS = 8

const editOpen = ref(false)
const showAll = ref(false)
const setupDate = ref('')
const setupError = ref<string | null>(null)
const { busy: settingUp, error: setupActionError, run } = useStoreAction(() => store.actionError)

watch(
  () => props.vin,
  (vin) => {
    if (vin) store.fetch(vin)
  },
  { immediate: true },
)

const warranty = computed(() => store.warranty)
const started = computed(() => warranty.value?.registrationDate != null)
const sorted = computed(() => sortCovers(warranty.value?.covers ?? []))
const featured = computed(() =>
  (warranty.value?.covers ?? []).filter((c) => c.featured && c.status !== 'notCovered'),
)
const rows = computed(() => sorted.value.filter((c) => !featured.value.includes(c)))
const phoneRows = computed(() => (showAll.value ? rows.value : rows.value.slice(0, PHONE_ROWS)))

function distance(km: number | null): string {
  return km == null ? '-' : (units.value.format('distance', km, ODOMETER_FORMAT) ?? '-')
}

function date(value: string | null): string {
  return value ? formatDate(value, COVER_DATE) : '-'
}

function limit(c: WarrantyCover): string {
  if (c.months === 0) return t('warranty.notCovered')
  const time =
    c.months % 12 === 0 && c.months >= 24
      ? t('warranty.limitYears', c.months / 12)
      : t('warranty.limitMonths', c.months)
  return c.distanceKm != null ? `${time} / ${distance(c.distanceKm)}` : time
}

function ends(c: WarrantyCover): string {
  if (c.status === 'notCovered' || !c.endsOn) return '-'
  if (c.status === 'expired')
    return c.endedBy === 'distance'
      ? t('warranty.endedAt', { distance: distance(c.distanceKm) })
      : t('warranty.ended', { when: date(c.endsOn) })
  return c.distanceKm != null
    ? t('warranty.endsShort', { when: date(c.endsOn), distance: distance(c.distanceKm) })
    : date(c.endsOn)
}

/** Time left, as a calendar counts it: "22 days", "8 mo 22 d" or "2 yr 8 mo". */
function timeLeft(c: WarrantyCover): string | null {
  if (c.daysRemaining == null || !c.endsOn || c.daysRemaining < 0) return null
  if (c.daysRemaining === 0) return t('warranty.endsToday')
  return formatTimeSpan(t, todayUtc(), utcDay(c.endsOn))
}

// What is left of a cover: the time, and the distance where the cover has a mileage limit.
function leftParts(c: WarrantyCover): string[] {
  if (c.status === 'expired' || c.status === 'notCovered') return []
  const parts = [timeLeft(c)]
  if (c.kmRemaining != null) parts.push(distance(c.kmRemaining))
  return parts.filter((part): part is string => !!part)
}

function left(c: WarrantyCover): string {
  return leftParts(c).join(' · ') || '-'
}

function expiredNote(c: WarrantyCover): string | null {
  return c.status === 'expired' && c.endedBy === 'distance' ? t('warranty.endedByDistance') : null
}

function featuredEnds(c: WarrantyCover): string {
  return c.distanceKm != null
    ? t('warranty.endsOnOrAt', { when: date(c.endsOn), distance: distance(c.distanceKm) })
    : t('warranty.endsOn', { when: date(c.endsOn) })
}

function featuredLimit(c: WarrantyCover): string {
  return c.distanceKm != null ? limit(c) : `${limit(c)}, ${t('warranty.anyMileage')}`
}

function bars(c: WarrantyCover) {
  const out: { key: string; icon: string; used: number; text: string }[] = []
  if (c.timeUsed != null && timeLeft(c))
    out.push({
      key: 'time',
      icon: 'calendar-days',
      used: c.timeUsed,
      text: t('warranty.left', { amount: timeLeft(c) }),
    })
  if (c.distanceUsed != null && c.kmRemaining != null && c.kmRemaining > 0)
    out.push({
      key: 'distance',
      icon: 'road',
      used: c.distanceUsed,
      text: t('warranty.left', { amount: distance(c.kmRemaining) }),
    })
  return out
}

async function startTracking() {
  setupError.value = null
  if (!setupDate.value) {
    setupError.value = t('warranty.setup.required')
    return
  }
  await run(() =>
    store.setDates(props.vin, {
      registrationDate: setupDate.value,
      deliveryDate: warranty.value?.deliveryDate?.slice(0, 10) ?? null,
      deliveryOdometerKm: warranty.value?.deliveryOdometerKm ?? null,
    }),
  )
}
</script>

<template>
  <section class="warranty" aria-labelledby="warranty-title">
    <div class="warranty__head">
      <div>
        <h2 id="warranty-title" class="warranty__title">{{ t('warranty.title') }}</h2>
        <p v-if="started && warranty" class="warranty__start">
          {{ t('warranty.starts') }}
          <button
            type="button"
            class="warranty__chip warranty__chip--button"
            :title="t('warranty.editStart')"
            @click="editOpen = true"
          >
            {{ date(warranty.registrationDate) }}
            <font-awesome-icon icon="pen-to-square" />
          </button>
          {{ t('warranty.firstRegistration') }}
          <template v-if="warranty.odometerKm != null">
            · {{ t('warranty.now') }}
            <span class="warranty__chip">{{ distance(warranty.odometerKm) }}</span>
          </template>
        </p>
      </div>
      <button v-if="started" class="btn btn-outline-secondary btn-sm" @click="editOpen = true">
        {{ t('warranty.edit') }}
      </button>
    </div>

    <div v-if="store.loadError" class="empty-state text-danger" role="alert">
      {{ errorMessage(store.loadError) }}
    </div>

    <form
      v-else-if="warranty && !started"
      class="warranty-card warranty-card--setup"
      @submit.prevent="startTracking"
    >
      <span class="warranty-card__head">
        <span class="warranty-card__name">{{ t('warranty.setup.title') }}</span>
        <span class="warranty-badge">{{ t('warranty.setup.badge') }}</span>
      </span>
      <span class="warranty-card__meta">{{ t('warranty.setup.text') }}</span>
      <div class="form-group">
        <label for="warranty-setup-date">{{ t('warranty.setup.registrationDate') }}</label>
        <input id="warranty-setup-date" v-model="setupDate" type="date" class="form-control" />
      </div>
      <div class="warranty-card__actions">
        <button type="submit" class="btn btn-primary btn-sm" :disabled="settingUp">
          {{ t('warranty.setup.start') }}
        </button>
        <span class="warranty-card__meta">{{ t('warranty.setup.hint') }}</span>
      </div>
      <p v-if="setupError || setupActionError" class="text-danger text-sm" role="alert">
        {{ setupError ?? setupActionError }}
      </p>
    </form>

    <template v-else-if="warranty && started">
      <div v-if="featured.length" class="warranty__featured">
        <article
          v-for="c in featured"
          :key="c.id"
          class="warranty-card"
          :class="`warranty-card--${c.status}`"
        >
          <span class="warranty-card__head">
            <span class="warranty-card__name warranty-card__name--big">{{ c.name }}</span>
            <span class="warranty-badge">{{ t(`warranty.status.${c.status}`) }}</span>
          </span>
          <span class="warranty-card__meta">{{ featuredLimit(c) }}</span>
          <span v-for="bar in bars(c)" :key="bar.key" class="warranty-card__bar-row">
            <font-awesome-icon :icon="bar.icon" class="warranty-card__bar-icon" />
            <span class="warranty-card__bar" role="presentation">
              <span
                class="warranty-card__bar-fill"
                :style="{ width: `${Math.max(bar.used * 100, 2)}%` }"
              />
            </span>
            <span class="warranty-card__bar-text">{{ bar.text }}</span>
          </span>
          <span class="warranty-card__meta">{{
            c.status === 'expired' ? ends(c) : featuredEnds(c)
          }}</span>
          <span v-if="c.notes" class="warranty-card__meta">{{ c.notes }}</span>
        </article>
      </div>

      <table v-if="rows.length" class="warranty-table">
        <thead>
          <tr>
            <th scope="col">{{ t('warranty.columns.cover') }}</th>
            <th scope="col">{{ t('warranty.columns.limit') }}</th>
            <th scope="col">{{ t('warranty.columns.ends') }}</th>
            <th scope="col" class="num">{{ t('warranty.columns.left') }}</th>
            <th scope="col" class="num">{{ t('warranty.columns.status') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="c in rows" :key="c.id" :class="`warranty-row--${c.status}`">
            <td>
              {{ c.name }}
              <span v-if="c.notes" class="warranty-row__note">{{ c.notes }}</span>
            </td>
            <td>{{ limit(c) }}</td>
            <td>
              {{ ends(c) }}
              <span v-if="expiredNote(c)" class="warranty-row__note">{{ expiredNote(c) }}</span>
            </td>
            <td class="num">{{ left(c) }}</td>
            <td class="num">
              <span class="warranty-badge">{{ t(`warranty.status.${c.status}`) }}</span>
            </td>
          </tr>
        </tbody>
      </table>

      <ul v-if="rows.length" class="warranty-list">
        <li v-for="c in phoneRows" :key="c.id" :class="`warranty-row--${c.status}`">
          <span class="warranty-list__main">
            <strong>{{ c.name }}</strong>
            <span class="warranty-row__note">
              {{ limit(c) }}<template v-if="c.status !== 'notCovered'"> · {{ ends(c) }}</template>
            </span>
            <span v-if="c.notes" class="warranty-row__note">{{ c.notes }}</span>
          </span>
          <span class="warranty-list__side">
            <span class="warranty-badge">{{ t(`warranty.status.${c.status}`) }}</span>
            <!-- One line each, so the cover's name keeps its room on a narrow screen. -->
            <span v-for="part in leftParts(c)" :key="part" class="warranty-row__note">{{
              part
            }}</span>
          </span>
        </li>
        <li v-if="rows.length > PHONE_ROWS" class="warranty-list__more">
          <button type="button" class="btn btn-link btn-sm" @click="showAll = !showAll">
            {{ showAll ? t('warranty.showFewer') : t('warranty.showAll', { n: rows.length }) }}
          </button>
        </li>
      </ul>

      <p class="warranty__footnote">{{ t('warranty.footnote') }}</p>
    </template>

    <WarrantyEditModal :open="editOpen" :vin="vin" @close="editOpen = false" />
  </section>
</template>

<style scoped>
.warranty {
  margin-top: 2rem;
}

.warranty__head {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: flex-end;
  justify-content: space-between;
  margin-bottom: 0.75rem;
}

.warranty__title {
  margin: 0;
  font-size: 1.25rem;
}

.warranty__start {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  align-items: center;
  margin: 0.25rem 0 0;
  font-size: 0.85rem;
  color: var(--color-text-muted);
}

.warranty__chip {
  display: inline-flex;
  gap: 0.35rem;
  align-items: center;
  padding: 0.1rem 0.6rem;
  font: inherit;
  color: var(--color-text);
  background: var(--color-surface-2);
  border: 1px solid var(--color-border);
  border-radius: 999px;
}

.warranty__chip--button {
  cursor: pointer;
}

.warranty__chip--button svg {
  font-size: 0.75em;
  color: var(--color-primary);
}

.warranty__featured {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 18rem), 1fr));
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.warranty-card {
  --tone: var(--color-success, #22c55e);

  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  padding: 0.875rem 1rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-left: 4px solid var(--tone);
  border-radius: var(--radius);
}

.warranty-card--setup {
  --tone: var(--color-primary);

  max-width: 34rem;
}

.warranty-card__head {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  justify-content: space-between;
}

.warranty-card__name {
  font-weight: 600;
}

.warranty-card__name--big {
  font-size: 1.05rem;
}

.warranty-card__meta {
  font-size: 0.8rem;
  color: var(--color-text-muted);
}

.warranty-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
  align-items: center;
}

.warranty-card__bar-row {
  display: grid;
  grid-template-columns: 1rem minmax(0, 1fr) auto;
  gap: 0.5rem;
  align-items: center;
  font-size: 0.8rem;
}

.warranty-card__bar-icon {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.warranty-card__bar {
  height: 0.5rem;
  overflow: hidden;
  background: var(--color-surface-2);
  border-radius: 999px;
}

.warranty-card__bar-fill {
  display: block;
  height: 100%;
  background: var(--tone);
  border-radius: 999px;
}

.warranty-badge {
  flex-shrink: 0;
  padding: 0.125rem 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--tone);
  white-space: nowrap;
  background: color-mix(in srgb, var(--tone) 15%, transparent);
  border-radius: 999px;
}

.warranty-card--endingSoon,
.warranty-row--endingSoon {
  --tone: var(--color-warning, #f59e0b);
}

.warranty-card--expired,
.warranty-row--expired {
  --tone: var(--color-danger, #ef4444);
}

.warranty-card--unknown,
.warranty-row--unknown,
.warranty-row--notCovered {
  --tone: var(--color-text-muted);
}

.warranty-row--active {
  --tone: var(--color-success, #22c55e);
}

.warranty-row--expired {
  opacity: 0.75;
}

.warranty-table {
  width: 100%;
  overflow: hidden;
  font-size: 0.875rem;
  border-spacing: 0;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
}

.warranty-table th {
  padding: 0.55rem 0.9rem;
  font-size: 0.7rem;
  font-weight: 600;
  color: var(--color-text-muted);
  text-align: left;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  border-bottom: 1px solid var(--color-border);
}

.warranty-table td {
  padding: 0.55rem 0.9rem;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border);
}

.warranty-table tr:last-child td {
  border-bottom: 0;
}

.warranty-table .num {
  text-align: right;
  white-space: nowrap;
}

.warranty-table td:nth-child(2) {
  white-space: nowrap;
}

.warranty-row__note {
  display: block;
  font-size: 0.78rem;
  color: var(--color-text-muted);
}

.warranty-list {
  display: none;
  padding: 0;
  margin: 0;
  list-style: none;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
}

.warranty-list li {
  display: flex;
  gap: 0.6rem;
  justify-content: space-between;
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid var(--color-border);
  border-left: 3px solid var(--tone);
}

.warranty-list li:last-child {
  border-bottom: 0;
}

.warranty-list__main {
  min-width: 0;
}

.warranty-list__side {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  align-items: flex-end;
  text-align: right;
}

.warranty-list__side .warranty-row__note {
  white-space: nowrap;
}

.warranty-list .warranty-list__more {
  justify-content: center;
  border-left-color: transparent;
}

.warranty__footnote {
  margin: 0.6rem 0 0;
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

@media (width <= 700px) {
  .warranty-table {
    display: none;
  }

  .warranty-list {
    display: block;
  }
}
</style>
