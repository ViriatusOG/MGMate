import type { WarrantyCover, WarrantyStatus } from '@/services/warrantyApi'

const STATUS_ORDER: Record<WarrantyStatus, number> = {
  endingSoon: 0,
  active: 0,
  unknown: 1,
  expired: 2,
  notCovered: 3,
}

/**
 * Covers in the order the page lists them: what ends first at the top, expired ones after the
 * running ones, and parts that are not covered last. Same end date: the saved order.
 */
export function sortCovers(covers: readonly WarrantyCover[]): WarrantyCover[] {
  return [...covers].sort((a, b) => {
    const byStatus = STATUS_ORDER[a.status] - STATUS_ORDER[b.status]
    if (byStatus !== 0) return byStatus
    const byEnd = (a.endsOn ?? '').localeCompare(b.endsOn ?? '')
    return byEnd !== 0 ? byEnd : a.sortOrder - b.sortOrder
  })
}

/** Whole calendar months and days from `from` to `to`, both plain days. */
export function monthsAndDays(from: Date, to: Date): { months: number; days: number } {
  let months =
    (to.getUTCFullYear() - from.getUTCFullYear()) * 12 + (to.getUTCMonth() - from.getUTCMonth())
  let anchor = addUtcMonths(from, months)
  if (anchor > to) {
    months--
    anchor = addUtcMonths(from, months)
  }
  const days = Math.round((to.getTime() - anchor.getTime()) / 86_400_000)
  return { months, days }
}

function addUtcMonths(date: Date, months: number): Date {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + months, 1))
  const lastDay = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 0)).getUTCDate()
  d.setUTCDate(Math.min(date.getUTCDate(), lastDay))
  return d
}

/** Today as a plain UTC day, which is how the API sends end dates. */
export function todayUtc(now: Date = new Date()): Date {
  return new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()))
}

/** End dates are midnight UTC: show them as that calendar day wherever the browser is. */
export const COVER_DATE: Intl.DateTimeFormatOptions = {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
}

/** A plain day for a date input, yyyy-mm-dd, from an API date. */
export function toDateInput(value: string | null): string {
  return value ? value.slice(0, 10) : ''
}
