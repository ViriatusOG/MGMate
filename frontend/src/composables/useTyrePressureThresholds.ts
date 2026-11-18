import { computed, ref } from 'vue'
import { useUiSettingsStore, type TyreSeason } from '@/stores/settingsUi'
import { vehicleApi, type TyrePressureThresholds } from '@/services/vehicleApi'

export type { TyrePressureThresholds }

export const DEFAULT_TYRE_PRESSURE_THRESHOLDS: TyrePressureThresholds = {
  lowBar: 2.2,
  goodBar: 2.6,
  highBar: 3.2,
  dropBar: 0.15,
  // The MG4 EV handbook's cold pressure, front and rear, for every wheel size.
  recommendedBar: 2.5,
  recommendedFrontBar: 2.5,
  recommendedRearBar: 2.5,
  defaultRecommendedBar: 2.5,
  season: null,
}

// Module-scoped so every component/composable calling useTyrePressureThresholds() shares the
// same fetch and the same reactive value, instead of each issuing its own request.
const thresholds = ref<TyrePressureThresholds>({ ...DEFAULT_TYRE_PRESSURE_THRESHOLDS })
let fetchPromise: Promise<void> | null = null

function load(): Promise<void> {
  if (!fetchPromise) {
    // Started inside a promise so a client without the call fails like a failed request would.
    fetchPromise = Promise.resolve()
      .then(() => vehicleApi.tyrePressureThresholds())
      .then((result) => {
        thresholds.value = result
      })
      .catch(() => {
        // Keep defaults on failure (e.g. offline, not yet authenticated); retry on next call.
        fetchPromise = null
      })
  }
  return fetchPromise
}

export function useTyrePressureThresholds() {
  void load()
  return thresholds
}

export type PressureVariant = 'ok' | 'warning' | 'danger' | 'unknown'

export function pressureVariant(bar: number | null, t: TyrePressureThresholds): PressureVariant {
  if (bar === null) return 'unknown'
  if (bar < t.lowBar) return 'danger'
  if (bar < t.goodBar) return 'warning'
  if (bar > t.highBar) return 'danger'
  return 'ok'
}

export interface RecommendedTyrePressure {
  /** The season of the pressures the account set; null when the deployment's are in use. */
  season: TyreSeason | null
  frontBar: number
  rearBar: number
}

/**
 * The cold pressures the tyres should be at now: the pair the account set under Settings > Your car
 * for the season it switched to, or else the deployment's (MG's 2.5 bar unless configured).
 */
export function useRecommendedTyrePressure() {
  const thresholds = useTyrePressureThresholds()
  const ui = useUiSettingsStore()
  return computed((): RecommendedTyrePressure => {
    const own = ui.tyrePressures
    if (own) return { season: own.season, ...own[own.season] }
    const bar = thresholds.value.defaultRecommendedBar
    return { season: null, frontBar: bar, rearBar: bar }
  })
}
