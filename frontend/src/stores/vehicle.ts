import { defineStore } from 'pinia'
import { ref, shallowRef, computed, nextTick } from 'vue'
import {
  vehicleApi,
  type Vehicle,
  type VehicleType,
  type TelemetrySnapshot,
  type DailyStat,
  type Trip,
  type TripSummary,
  type CommandResult,
} from '@/services/vehicleApi'
import { browserTimeZone } from '@/utils/dates'
import { useUiSettingsStore } from '@/stores/settingsUi'
import { useLoadingTracker } from '@/composables/useLoadingTracker'

// Defined with the API contract it comes from; re-exported here because every view reads the
// vehicle's type through this store.
export type { VehicleType }

/**
 * Numbers the calls of one fetch action so only the newest one writes the store. Switching the
 * map's period twice quickly sends two requests; without this the first answer arriving last
 * would show the wrong period. `supersede` drops the call in flight without starting a new one.
 */
function latestCall() {
  let latest = 0
  return {
    begin: () => ++latest,
    isCurrent: (call: number) => call === latest,
    supersede: () => {
      latest++
    },
  }
}

export const useVehicleStore = defineStore('vehicle', () => {
  const uiSettings = useUiSettingsStore()

  const vehicles = ref<Vehicle[]>([])
  const currentStatus = ref<TelemetrySnapshot | null>(null)
  const vehicleConfig = ref<Record<string, string>>({})
  // shallowRef: these are only ever replaced wholesale on fetch, never mutated
  // field-by-field, so deep reactivity on every GPS point/telemetry snapshot is wasted work.
  const dailyStats = shallowRef<DailyStat[]>([])
  const trips = shallowRef<Trip[]>([])
  // Separate from `trips`, which the map fills with a period of trips and all their fixes: the
  // dashboard needs only the newest trip, and the statistics page only the figures of a period.
  const latestTrip = shallowRef<Trip | null>(null)
  const tripSummaries = shallowRef<TripSummary[]>([])
  const { loading, withLoading } = useLoadingTracker()
  const sendingCount = ref(0)
  const anySending = computed(() => sendingCount.value > 0)
  // Each fetch action gets its own error ref so concurrent calls (e.g. Promise.all on
  // mount) can't have one action's success silently overwrite another action's error.
  const vehiclesError = ref<Error | null>(null)
  const statusError = ref<Error | null>(null)
  const dailyStatsError = ref<Error | null>(null)
  const tripsError = ref<Error | null>(null)
  const latestTripError = ref<Error | null>(null)
  const tripSummariesError = ref<Error | null>(null)
  const lastUpdated = ref<Date | null>(null)

  // Reactive one-shot flags set for a single tick when specific state transitions occur.
  const tripJustCompleted = ref(false)
  const chargingJustCompleted = ref(false)

  // The gateway's latest answer to a command. Each answer is a new object, so a watcher sees it
  // even when it repeats the previous one; useVehicleCommand picks out the ones it is waiting on.
  const lastCommandResult = shallowRef<CommandResult | null>(null)

  // The vehicle list rarely changes during a session, so every view (Dashboard, Map, Statistics)
  // calling fetchVehicles() on mount would otherwise re-fetch it on every navigation. Skip the
  // request once it's already populated unless the caller explicitly asks for a fresh copy.
  async function fetchVehicles(force = false) {
    if (!force && vehicles.value.length > 0) return
    await withLoading(vehiclesError, async () => {
      vehicles.value = await vehicleApi.list()
    })
  }

  const statusCalls = latestCall()
  const dailyStatsCalls = latestCall()
  const tripsCalls = latestCall()
  const latestTripCalls = latestCall()
  const tripSummariesCalls = latestCall()

  async function fetchStatus(vin: string) {
    const call = statusCalls.begin()
    await withLoading(
      statusError,
      async () => {
        const status = await vehicleApi.status(vin)
        if (!statusCalls.isCurrent(call)) return
        currentStatus.value = status
        lastUpdated.value = new Date()
      },
      () => statusCalls.isCurrent(call),
    )
  }

  async function fetchConfig(vin: string) {
    try {
      vehicleConfig.value = await vehicleApi.config(vin)
    } catch {
      // config may not exist yet
    }
  }

  async function fetchDailyStats(vin: string, from: string, to: string) {
    const call = dailyStatsCalls.begin()
    await withLoading(
      dailyStatsError,
      async () => {
        const stats = await vehicleApi.dailyStats(vin, from, to, browserTimeZone())
        if (dailyStatsCalls.isCurrent(call)) dailyStats.value = stats
      },
      () => dailyStatsCalls.isCurrent(call),
    )
  }

  function applyLiveStatus(snapshot: TelemetrySnapshot) {
    const prev = currentStatus.value

    // Charging complete: was charging, cable still connected, now stopped
    if (
      prev?.isCharging === true &&
      snapshot.isCharging === false &&
      snapshot.chargerConnected === true
    ) {
      chargingJustCompleted.value = true
      nextTick(() => {
        chargingJustCompleted.value = false
      })
    }

    // A pushed snapshot is newer than any status request still in flight, so that answer must
    // not replace it when it lands.
    statusCalls.supersede()
    currentStatus.value = snapshot
    lastUpdated.value = new Date()
  }

  function notifyTripCompleted() {
    tripJustCompleted.value = true
    nextTick(() => {
      tripJustCompleted.value = false
    })
  }

  function applyCommandResult(result: CommandResult) {
    lastCommandResult.value = result
  }

  async function fetchTrips(vin: string, from?: string, to?: string) {
    const call = tripsCalls.begin()
    await withLoading(
      tripsError,
      async () => {
        const result = await vehicleApi.trips(vin, from, to)
        if (tripsCalls.isCurrent(call)) trips.value = result
      },
      () => tripsCalls.isCurrent(call),
    )
  }

  async function fetchLatestTrip(vin: string) {
    const call = latestTripCalls.begin()
    await withLoading(
      latestTripError,
      async () => {
        const result = (await vehicleApi.latestTrip(vin)) ?? null
        if (latestTripCalls.isCurrent(call)) latestTrip.value = result
      },
      () => latestTripCalls.isCurrent(call),
    )
  }

  async function fetchTripSummaries(vin: string, from?: string, to?: string) {
    const call = tripSummariesCalls.begin()
    await withLoading(
      tripSummariesError,
      async () => {
        const result = await vehicleApi.tripSummaries(vin, from, to)
        if (tripSummariesCalls.isCurrent(call)) tripSummaries.value = result
      },
      () => tripSummariesCalls.isCurrent(call),
    )
  }

  // MGMate follows one car. Every view needs the same one, so "the first vehicle" is
  // resolved here instead of being re-derived from the list by index in each component.
  const activeVehicle = computed((): Vehicle | null => vehicles.value[0] ?? null)
  const activeVin = computed((): string | null => activeVehicle.value?.vin ?? null)

  // Detected by the API from the vehicle's reported hardware version, so the rule lives in one
  // place rather than once per client. Unknown until a vehicle has been fetched.
  const detectedVehicleType = computed(
    (): VehicleType => activeVehicle.value?.vehicleType ?? 'unknown',
  )

  // What every view should treat the car as: the user's manual override when set, otherwise
  // the detected type. Defined once here rather than in each view.
  const effectiveVehicleType = computed((): VehicleType => {
    const override = uiSettings.vehicleTypeOverride
    return override === 'auto' ? detectedVehicleType.value : override
  })

  // The deployment's answer for how big the traction battery really is, or null when it has not
  // been told. Read alongside the snapshot's own kWh figures in utils/energy.
  const hvBatteryCapacityKwh = computed(
    (): number | null => activeVehicle.value?.hvBatteryCapacityKwh ?? null,
  )

  /** The plate just saved for a car, so every page shows it without asking for the list again. */
  function setLicensePlate(vin: string, licensePlate: string | null) {
    vehicles.value = vehicles.value.map((v) => (v.vin === vin ? { ...v, licensePlate } : v))
  }

  return {
    vehicles,
    setLicensePlate,
    activeVehicle,
    activeVin,
    currentStatus,
    vehicleConfig,
    detectedVehicleType,
    effectiveVehicleType,
    hvBatteryCapacityKwh,
    dailyStats,
    trips,
    latestTrip,
    tripSummaries,
    loading,
    anySending,
    sendingCount,
    vehiclesError,
    statusError,
    dailyStatsError,
    tripsError,
    latestTripError,
    tripSummariesError,
    lastUpdated,
    tripJustCompleted,
    chargingJustCompleted,
    lastCommandResult,
    fetchVehicles,
    fetchStatus,
    fetchConfig,
    fetchDailyStats,
    fetchTrips,
    fetchLatestTrip,
    fetchTripSummaries,
    applyLiveStatus,
    notifyTripCompleted,
    applyCommandResult,
  }
})
