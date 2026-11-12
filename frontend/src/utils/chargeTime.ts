import { formatDurationMs } from './tripRows'

type Translate = (key: string, params?: Record<string, unknown>) => string

/**
 * The gateway reports the time left on a charge in SECONDS (MG's own minutes times 60, published
 * as a Home Assistant duration in "s"), so 21060 is 5h 51m, not 351 hours. Nothing, or zero,
 * means the car gave no estimate.
 */
export function chargeSecondsLeft(seconds: number | null | undefined): number | null {
  return seconds != null && Number.isFinite(seconds) && seconds > 0 ? seconds : null
}

/** The time left on a charge, the way the MG app shows it: "45 min" or "5h 51m". */
export function formatChargeTimeLeft(
  seconds: number | null | undefined,
  t: Translate,
): string | null {
  const left = chargeSecondsLeft(seconds)
  return left === null ? null : formatDurationMs(left * 1000, t)
}
