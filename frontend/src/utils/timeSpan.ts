import { monthsAndDays } from '@/utils/warranty'

type Translate = (key: string, named: Record<string, unknown> | number) => string

/**
 * How long from one plain day to a later one, the way a calendar counts it: "22 days" under a
 * month, "8 mo 22 d" under a year, "2 yr 8 mo" beyond. Both dates are midnight UTC.
 */
export function formatTimeSpan(t: Translate, from: Date, to: Date): string {
  const { months, days } = monthsAndDays(from, to)
  if (months === 0) return t('timeSpan.days', days)
  if (months < 12)
    return days === 0
      ? t('timeSpan.months', { n: months })
      : t('timeSpan.monthsDays', { months, days })
  const years = Math.floor(months / 12)
  const rest = months % 12
  return rest === 0
    ? t('timeSpan.years', years)
    : t('timeSpan.yearsMonths', { years, months: rest })
}

/** A plain day from an API date or a Date, as midnight UTC. */
export function utcDay(value: string | Date): Date {
  const d = new Date(value)
  return new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()))
}
