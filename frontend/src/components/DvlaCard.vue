<script setup lang="ts">
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVehicleStore } from '@/stores/vehicle'
import { useDvlaStore } from '@/stores/dvla'
import { formatDate, formatDateTime } from '@/utils/format'
import { formatPlateAs } from '@/utils/licensePlate'
import { parseLocalDate } from '@/utils/statsPeriod'
import { formatTimeSpan, utcDay } from '@/utils/timeSpan'
import type { BadgeVariant } from '@/utils/levels'
import ExpandableStatusCard from './ExpandableStatusCard.vue'
import StatusBadge from './StatusBadge.vue'

// The UK registration: whether the car is taxed and until when, its MOT, and the rest of what the
// DVLA holds. The card shows the tax status; the dialog shows everything.
const { t } = useI18n()
const vehicleStore = useVehicleStore()
const store = useDvlaStore()

const vin = computed(() => vehicleStore.activeVin)

watch(
  vin,
  (v) => {
    if (v) store.ensureLoaded(v)
  },
  { immediate: true },
)

const DAY: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' }

const plate = computed(() =>
  store.status?.licensePlate ? formatPlateAs('gb', store.status.licensePlate) : null,
)
const info = computed(() => store.status?.info ?? null)
const known = computed(() => (info.value?.found ? info.value : null))
const asked = computed(() => !!info.value?.fetchedAt)

function day(text: string | null | undefined): string | null {
  const date = parseLocalDate(text)
  return date ? formatDate(date, DAY) : null
}

// "8 mo 16 d left", counted from today to the date.
function left(text: string | null | undefined): string | null {
  const date = parseLocalDate(text)
  if (!date) return null
  const to = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()))
  const today = utcDay(new Date(Date.now() - new Date().getTimezoneOffset() * 60_000))
  if (to < today) return null
  if (to.getTime() === today.getTime()) return t('dvla.today')
  return t('dvla.left', { span: formatTimeSpan(t, today, to) })
}

function is(status: string | null | undefined, expected: string): boolean {
  return status?.trim().toLowerCase() === expected
}

const taxed = computed(() => is(known.value?.taxStatus, 'taxed'))
const untaxed = computed(() => is(known.value?.taxStatus, 'untaxed'))
const taxSoon = computed(() => {
  const days = known.value?.taxDaysLeft
  return taxed.value && days !== null && days !== undefined && days <= 30
})

const taxVariant = computed((): BadgeVariant => {
  if (untaxed.value) return 'danger'
  if (taxSoon.value) return 'warning'
  return taxed.value ? 'success' : 'secondary'
})

const taxLine = computed(() => {
  const k = known.value
  const date = day(k?.taxDueDate)
  if (!k || !date) return null
  if (untaxed.value) return t('dvla.taxRanOut', { date })
  const span = left(k.taxDueDate)
  return span ? `${t('dvla.due', { date })} · ${span}` : t('dvla.due', { date })
})

// The DVLA holds no MOT for a car too new to need one: the first falls due three years after
// first registration.
const motDate = computed(() => known.value?.motExpiryDate ?? null)
const motExpired = computed(() => {
  const k = known.value
  return is(k?.motStatus, 'not valid') || (k?.motDaysLeft ?? 0) < 0
})
const motSoon = computed(() => {
  const days = known.value?.motDaysLeft
  return !motExpired.value && days !== null && days !== undefined && days <= 30
})
const motNotNeeded = computed(() => !!known.value && !motDate.value && !motExpired.value)

const motBadge = computed(() => {
  if (motExpired.value) return t('dvla.motExpired')
  if (motNotNeeded.value) return t('dvla.motNotNeeded')
  return known.value?.motStatus ?? t('dvla.unknown')
})

const motVariant = computed((): BadgeVariant => {
  if (motExpired.value) return 'danger'
  if (motSoon.value) return 'warning'
  return is(known.value?.motStatus, 'valid') ? 'success' : 'secondary'
})

const firstMot = computed(() => store.status?.firstMotDue ?? null)

const motLine = computed(() => {
  const date = day(motDate.value)
  if (date) {
    if (motExpired.value) return t('dvla.motRanOut', { date })
    const span = left(motDate.value)
    return span ? `${t('dvla.due', { date })} · ${span}` : t('dvla.due', { date })
  }
  if (motExpired.value) return t('dvla.motNone')
  const first = day(firstMot.value)
  if (!first) return t('dvla.motFirstUnknown')
  const span = left(firstMot.value)
  return span
    ? `${t('dvla.firstMotDue', { date: first })} · ${span}`
    : t('dvla.firstMotDue', { date: first })
})

const value = computed(() => {
  // A row without an answer is a lookup that failed, not a plate the DVLA does not know.
  if (!info.value || !asked.value) return t('dvla.notLookedUp')
  if (!info.value.found) return t('dvla.plateUnknown')
  return known.value?.taxStatus ?? t('dvla.unknown')
})

const note = computed(() => {
  const k = known.value
  if (!k) return plate.value ? t('dvla.plate', { plate: plate.value }) : undefined
  const parts: string[] = []
  const tax = day(k.taxDueDate)
  if (tax) parts.push(t(untaxed.value ? 'dvla.note.taxRanOut' : 'dvla.note.taxDue', { date: tax }))
  const mot = day(motDate.value)
  const first = day(firstMot.value)
  if (motExpired.value) parts.push(t('dvla.note.motExpired'))
  else if (mot) parts.push(t('dvla.note.motDue', { date: mot }))
  else if (first) parts.push(t('dvla.note.firstMotDue', { date: first }))
  return parts.join(' · ') || undefined
})

// Red for what keeps the car off the road, amber for what runs out within the month.
const variant = computed((): 'success' | 'warning' | 'danger' | undefined => {
  if (!known.value) return undefined
  if (untaxed.value || motExpired.value) return 'danger'
  if (taxSoon.value || motSoon.value) return 'warning'
  return taxed.value ? 'success' : undefined
})

// "ELECTRICITY" as the DVLA shouts it reads "Electricity".
function calm(text: unknown): string | null {
  if (typeof text !== 'string' || !text.trim()) return null
  const lower = text.trim().toLowerCase()
  return lower.charAt(0).toUpperCase() + lower.slice(1)
}

function text(name: string): string | null {
  const v = known.value?.record[name]
  return typeof v === 'string' && v.trim() ? v.trim() : null
}

function number(name: string): number | null {
  const v = known.value?.record[name]
  return typeof v === 'number' ? v : null
}

function month(value: string | null): string | null {
  const date = parseLocalDate(value ? `${value}-01` : null)
  return date ? formatDate(date, { month: 'long', year: 'numeric' }) : null
}

// What the DVLA holds besides tax and MOT, each row only when the DVLA sent it.
const held = computed(() => {
  const k = known.value
  if (!k) return []
  const rows: { label: string; value: string | null }[] = [
    {
      label: t('dvla.held.registeredAs'),
      value:
        [k.make?.toUpperCase() ?? null, calm(k.colour), calm(k.fuelType)]
          .filter(Boolean)
          .join(' · ') || null,
    },
    { label: t('dvla.held.firstRegistered'), value: month(text('monthOfFirstRegistration')) },
    {
      label: t('dvla.held.yearOfManufacture'),
      value: number('yearOfManufacture')?.toString() ?? null,
    },
    { label: t('dvla.held.lastV5c'), value: day(text('dateOfLastV5CIssued')) },
    {
      label: t('dvla.held.co2'),
      value: number('co2Emissions') === null ? null : `${number('co2Emissions')} g/km`,
    },
    {
      label: t('dvla.held.engine'),
      value: number('engineCapacity') ? `${number('engineCapacity')} cc` : null,
    },
    { label: t('dvla.held.euroStatus'), value: text('euroStatus') },
    {
      label: t('dvla.held.typeApproval'),
      value: [text('typeApproval'), calm(text('wheelplan'))].filter(Boolean).join(' · ') || null,
    },
    {
      label: t('dvla.held.revenueWeight'),
      value: number('revenueWeight') ? `${number('revenueWeight')} kg` : null,
    },
    {
      label: t('dvla.held.export'),
      value:
        typeof k.record.markedForExport === 'boolean'
          ? t(k.record.markedForExport ? 'dvla.yes' : 'dvla.no')
          : null,
    },
  ]
  return rows.filter((r): r is { label: string; value: string } => r.value !== null)
})

const fetchedAt = computed(() =>
  info.value?.fetchedAt
    ? t('dvla.fetchedAt', { time: formatDateTime(info.value.fetchedAt) })
    : null,
)
</script>

<template>
  <ExpandableStatusCard
    icon="clipboard-check"
    :title="t('dvla.title')"
    :value="value"
    :note="note"
    :variant="variant"
  >
    <p v-if="info && asked && !info.found" class="card-info-desc">
      {{ t('dvla.notFound', { plate }) }}
    </p>
    <p v-else-if="!info || !asked" class="card-info-desc">{{ t('dvla.waiting') }}</p>

    <template v-if="known">
      <div class="dvla-row" data-testid="dvla-tax">
        <div class="dvla-row__text">
          <div class="dvla-row__title">{{ t('dvla.roadTax') }}</div>
          <div v-if="taxLine" class="dvla-row__line">{{ taxLine }}</div>
        </div>
        <StatusBadge :variant="taxVariant">{{ known.taxStatus ?? t('dvla.unknown') }}</StatusBadge>
      </div>
      <div class="dvla-row" data-testid="dvla-mot">
        <div class="dvla-row__text">
          <div class="dvla-row__title">{{ t('dvla.mot') }}</div>
          <div class="dvla-row__line">{{ motLine }}</div>
        </div>
        <StatusBadge :variant="motVariant">{{ motBadge }}</StatusBadge>
      </div>

      <template v-if="held.length > 0">
        <div class="detail-modal__section-title mt-3">{{ t('dvla.held.title') }}</div>
        <dl class="dvla-held">
          <template v-for="row in held" :key="row.label">
            <dt>{{ row.label }}</dt>
            <dd>{{ row.value }}</dd>
          </template>
        </dl>
      </template>
    </template>

    <p v-if="fetchedAt" class="rdw-card__meta">
      {{ fetchedAt }} · {{ t('dvla.source') }}<template v-if="plate"> · {{ plate }}</template>
    </p>
    <p v-if="info?.lastError" class="rdw-card__meta text-danger" :title="info.lastError">
      {{ t('dvla.lastAttemptFailed') }}
    </p>
  </ExpandableStatusCard>
</template>
