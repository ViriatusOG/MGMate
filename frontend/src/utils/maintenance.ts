import type { MaintenanceDueStatus, MaintenanceItem } from '@/services/maintenanceApi'
import { ODOMETER_FORMAT, type UnitFormatter } from '@/utils/units'

type Translate = (key: string, named?: Record<string, unknown>) => string

/**
 * The colour a due status is shown in, on the maintenance page, its detail dialog and the
 * dashboard card alike. An item with no service on record yet is information, not a warning.
 */
export function dueStatusVariant(
  status: MaintenanceDueStatus,
): 'success' | 'warning' | 'danger' | 'info' {
  if (status === 'overdue') return 'danger'
  if (status === 'dueSoon') return 'warning'
  if (status === 'ok') return 'success'
  return 'info'
}

/** "Every 15,000 km or every 12 months", built from whichever intervals the item defines. */
export function formatIntervalSummary(
  item: Pick<MaintenanceItem, 'intervalKm' | 'intervalMonths'>,
  t: Translate,
  units: UnitFormatter,
): string {
  const parts: string[] = []
  if (item.intervalKm != null)
    parts.push(
      t('maintenance.everyDistance', {
        distance: units.format('distance', item.intervalKm, ODOMETER_FORMAT),
      }),
    )
  if (item.intervalMonths != null)
    parts.push(t('maintenance.everyMonths', { months: item.intervalMonths }))
  return parts.join(` ${t('maintenance.or')} `)
}

const DAYS_PER_MONTH = 365.25 / 12

export interface DueProgress {
  /** Share of the interval used up, 0 to 1 (held at 1 once overdue). */
  used: number
  /** What is left before it is due; negative once overdue. Kilometres or days. */
  remaining: number
}

/**
 * How far through each interval an item is, for the due bars. Only intervals with both an
 * interval and a remaining figure from the server get a bar.
 */
export function dueProgress(
  item: Pick<MaintenanceItem, 'intervalKm' | 'intervalMonths' | 'kmRemaining' | 'daysRemaining'>,
): { distance: DueProgress | null; time: DueProgress | null } {
  const bar = (interval: number | null, remaining: number | null): DueProgress | null =>
    interval != null && interval > 0 && remaining != null
      ? { used: Math.max(0, Math.min(1, 1 - remaining / interval)), remaining }
      : null
  return {
    distance: bar(item.intervalKm, item.kmRemaining),
    time: bar(
      item.intervalMonths != null ? item.intervalMonths * DAYS_PER_MONTH : null,
      item.daysRemaining,
    ),
  }
}

export interface MaintenanceSuggestion {
  key: string
  intervalKm: number | null
  intervalMonths: number | null
}

/** Common items for an electric car, offered as a starting point in the add form. */
export const EV_MAINTENANCE_SUGGESTIONS: readonly MaintenanceSuggestion[] = [
  { key: 'service', intervalKm: 24000, intervalMonths: 12 },
  { key: 'mot', intervalKm: null, intervalMonths: 12 },
  { key: 'cabinFilter', intervalKm: 20000, intervalMonths: 12 },
  { key: 'brakeFluid', intervalKm: null, intervalMonths: 24 },
  { key: 'tyreRotation', intervalKm: 10000, intervalMonths: null },
  { key: 'coolant', intervalKm: null, intervalMonths: 48 },
  { key: 'wipers', intervalKm: null, intervalMonths: 12 },
  { key: 'battery12v', intervalKm: null, intervalMonths: 24 },
]
