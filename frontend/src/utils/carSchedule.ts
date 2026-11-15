import { timeOfDay } from '@/utils/dates'
import { formatTimeOfDay } from '@/utils/format'

type Translate = (key: string, named?: Record<string, unknown>) => string

/**
 * When a schedule the car keeps itself starts next. Those repeat every day at a time of day, so the
 * next start is today when that time is still to come, else tomorrow. The time is the car's own,
 * read here as the browser's, which is the same for a car at home.
 */
export function nextDailyStart(
  value: string | null | undefined,
  now: Date,
  t: Translate,
): string | null {
  const hhmm = timeOfDay(value)
  if (!hhmm) return null
  const [hours, minutes] = hhmm.split(':').map(Number) as [number, number]
  const todayAt = new Date(now)
  todayAt.setHours(hours, minutes, 0, 0)
  const time = formatTimeOfDay(hhmm)
  return todayAt > now
    ? t('climateSchedules.when.today', { time })
    : t('climateSchedules.when.tomorrow', { time })
}

/** The gateway reports the charging current as "Max" or "16A"; shown as "Max" or "16 A". */
export function chargeCurrentText(value: string | null | undefined, t: Translate): string | null {
  if (!value) return null
  if (value.toUpperCase() === 'MAX') return t('control.chargeLimitMax')
  const amps = /^(\d+)\s*A$/i.exec(value)
  return amps ? `${amps[1]} A` : value
}
