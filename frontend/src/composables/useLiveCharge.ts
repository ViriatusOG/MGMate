import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useVehicleStore } from '@/stores/vehicle'
import { activityApi, type ActivityTimeline } from '@/services/activityApi'
import { chargeSecondsLeft } from '@/utils/chargeTime'

const DAY_MS = 86_400_000

/**
 * The charge under way, as the Overview's battery card and the Charges page both show it: the last
 * 24 hours of activity (for where this charge began and the level line), a clock that ticks
 * between the car's reports, and the figures worked out from the two.
 */
export function useLiveCharge() {
  const store = useVehicleStore()
  const s = computed(() => store.currentStatus)
  const soc = computed(() => s.value?.evSocPercent ?? null)

  const timeline = ref<ActivityTimeline | null>(null)
  const now = ref(Date.now())

  async function loadDay() {
    const vin = store.activeVin
    if (!vin) return
    now.value = Date.now()
    try {
      timeline.value = await activityApi.timeline(
        vin,
        new Date(now.value - DAY_MS).toISOString(),
        new Date(now.value).toISOString(),
      )
    } catch {
      timeline.value = null
    }
  }

  // The day is read again every ten minutes and when charging starts or stops; in between the
  // live level is drawn on its end, and the clock ticks for "charging for" and the time left.
  let reload: ReturnType<typeof setInterval> | undefined
  let tick: ReturnType<typeof setInterval> | undefined
  onMounted(() => {
    void loadDay()
    reload = setInterval(loadDay, 10 * 60_000)
    tick = setInterval(() => (now.value = Date.now()), 30_000)
  })
  onUnmounted(() => {
    clearInterval(reload)
    clearInterval(tick)
  })
  watch(() => store.activeVin, loadDay)
  watch(
    () => s.value?.isCharging,
    (a, b) => {
      if (a !== b && b !== undefined) void loadDay()
    },
  )

  const charging = computed(() => s.value?.isCharging === true)

  const capacityKwh = computed(
    () => store.hvBatteryCapacityKwh ?? s.value?.hvTotalCapacityKwh ?? null,
  )

  // Where this charge began: the day's last "charging started", when no stop came after it (else it
  // belongs to an earlier charge), or failing that the car's own stamp, when it is newer than the
  // car's last end. A start level above the level now cannot be this charge's.
  const chargeStart = computed(() => {
    const last = [...(timeline.value?.events ?? [])]
      .reverse()
      .find((e) => e.kind === 'chargingStarted' || e.kind === 'chargingStopped')
    const started = last?.kind === 'chargingStarted' ? last : null
    const stampStart = s.value?.chargingLastStartAt ? Date.parse(s.value.chargingLastStartAt) : null
    const stampEnd = s.value?.chargingLastEndAt ? Date.parse(s.value.chargingLastEndAt) : null
    const stamp =
      stampStart !== null && (stampEnd === null || stampStart > stampEnd) ? stampStart : null
    const startSoc = started?.socPercent ?? null
    return {
      at: started ? Date.parse(started.at) : stamp,
      soc: startSoc !== null && soc.value !== null && startSoc <= soc.value ? startSoc : null,
    }
  })

  const powerKw = computed(() => {
    const x = s.value
    const hv = x?.hvPower ?? null
    if (hv !== null && hv !== 0) return Math.abs(hv)
    return x?.obcPowerThreePhase ?? x?.obcPowerSinglePhase ?? null
  })

  // The MG reports DC rapid charging only by its power: no home or public AC point gives over 22 kW.
  const chargeKind = computed(() => {
    const type = s.value?.chargingType?.toUpperCase() ?? ''
    if (type.includes('DC')) return 'dc'
    if (type.includes('AC')) return 'ac'
    if (powerKw.value === null) return null
    return powerKw.value > 22 ? 'dc' : 'ac'
  })

  // The car's own estimate (the one the MG app shows), which the gateway gives in seconds.
  const remainingSec = computed(() => chargeSecondsLeft(s.value?.remainingChargingTime))

  /** The energy this charge has put in so far, from the level it began on. */
  const addedKwh = computed(() =>
    chargeStart.value.soc !== null && soc.value !== null && capacityKwh.value
      ? Math.max(0, ((soc.value - chargeStart.value.soc) / 100) * capacityKwh.value)
      : null,
  )

  return {
    timeline,
    now,
    charging,
    capacityKwh,
    chargeStart,
    powerKw,
    chargeKind,
    remainingSec,
    addedKwh,
  }
}
