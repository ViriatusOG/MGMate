/** What a search of the trip or charge log asks for: words to find, and the days to look over. */
export interface LogSearchQuery {
  /** Words that must all appear somewhere in what is shown about an entry; empty finds all. */
  text: string
  /** The first and last day, as the date input gives them ("2026-11-07"). */
  from: string
  to: string
}

/** The longest stretch the log endpoints serve in one go. */
export const LONGEST_SEARCH_DAYS = 366

export type SearchRangeResult =
  { ok: true; from: Date; to: Date } | { ok: false; reason: 'missing' | 'backwards' | 'tooLong' }

function localDay(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) return null
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
  return Number.isNaN(date.getTime()) ? null : date
}

/** The local days a search covers, from the start of its first to the end of its last. */
export function searchRange(query: Pick<LogSearchQuery, 'from' | 'to'>): SearchRangeResult {
  const from = localDay(query.from)
  const last = localDay(query.to)
  if (!from || !last) return { ok: false, reason: 'missing' }
  if (last < from) return { ok: false, reason: 'backwards' }
  const to = new Date(last.getFullYear(), last.getMonth(), last.getDate() + 1)
  const days = Math.round((to.getTime() - from.getTime()) / 86_400_000)
  if (days > LONGEST_SEARCH_DAYS) return { ok: false, reason: 'tooLong' }
  return { ok: true, from, to }
}

/** "2026-11-07" for a local day, as a date input takes it. */
export function dateInputValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** A search over the year up to today, which is where a search starts. */
export function defaultSearch(today = new Date()): LogSearchQuery {
  const from = new Date(today.getFullYear() - 1, today.getMonth(), today.getDate() + 1)
  return { text: '', from: dateInputValue(from), to: dateInputValue(today) }
}

// Case and accents aside, so "sao" finds "São Paulo".
function fold(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLocaleLowerCase()
}

/** True when every word of the search appears in one or other of the texts. */
export function matchesText(
  texts: readonly (string | null | undefined)[],
  search: string,
): boolean {
  const words = fold(search).split(/\s+/).filter(Boolean)
  if (words.length === 0) return true
  const haystack = fold(texts.filter(Boolean).join(' \u0000 '))
  return words.every((word) => haystack.includes(word))
}
