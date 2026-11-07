/** A local day's key, "YYYY-MM-DD", as the calendar and the day headers share it. */
export function dayKey(date: Date): string {
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${m}-${d}`
}

export interface DayGroup<T> {
  key: string
  date: Date
  items: T[]
}

/**
 * Items grouped by the local day they started on, newest day first and newest item first within
 * a day, as both logs list them.
 */
export function groupByDay<T>(items: readonly T[], startedAt: (item: T) => string): DayGroup<T>[] {
  const groups = new Map<string, DayGroup<T>>()
  for (const item of items) {
    const start = new Date(startedAt(item))
    const key = dayKey(start)
    let group = groups.get(key)
    if (!group) {
      group = {
        key,
        date: new Date(start.getFullYear(), start.getMonth(), start.getDate()),
        items: [],
      }
      groups.set(key, group)
    }
    group.items.push(item)
  }
  const list = [...groups.values()]
  for (const g of list) {
    g.items.sort((a, b) => Date.parse(startedAt(b)) - Date.parse(startedAt(a)))
  }
  return list.sort((a, b) => b.date.getTime() - a.date.getTime())
}

/** The cells of a month for a Monday-first calendar: null for the blanks before the 1st. */
export function monthCells(month: Date): (number | null)[] {
  const first = new Date(month.getFullYear(), month.getMonth(), 1)
  const days = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate()
  const lead = (first.getDay() + 6) % 7
  return [...Array<null>(lead).fill(null), ...Array.from({ length: days }, (_, i) => i + 1)]
}
