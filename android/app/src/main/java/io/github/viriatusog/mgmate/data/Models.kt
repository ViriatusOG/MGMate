package io.github.viriatusog.mgmate.data

import kotlinx.serialization.Serializable

/** A car as GET /api/vehicles lists it. */
@Serializable
data class Vehicle(
    val id: Int,
    val vin: String,
    val model: String? = null,
    val series: String? = null,
    val supportsTargetSoc: Boolean = false,
    val licensePlate: String? = null,
)

/**
 * The newest reading the server holds for a car (GET /api/vehicles/{vin}/status). Reading it never
 * wakes the car: the server answers from its own database. Distances are in km and temperatures in
 * degrees Celsius, as the server stores them.
 */
@Serializable
data class CarStatus(
    val recordedAt: String? = null,
    val isLocked: Boolean? = null,
    val engineRunning: Boolean? = null,
    val climateOn: Boolean? = null,
    val climateMode: String? = null,
    val driverDoorOpen: Boolean? = null,
    val passengerDoorOpen: Boolean? = null,
    val rearLeftDoorOpen: Boolean? = null,
    val rearRightDoorOpen: Boolean? = null,
    val trunkOpen: Boolean? = null,
    val bonnetOpen: Boolean? = null,
    val driverWindowOpen: Boolean? = null,
    val passengerWindowOpen: Boolean? = null,
    val rearLeftWindowOpen: Boolean? = null,
    val rearRightWindowOpen: Boolean? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val interiorTemperature: Double? = null,
    val exteriorTemperature: Double? = null,
    val evSocPercent: Double? = null,
    val electricRangeKm: Double? = null,
    val isCharging: Boolean? = null,
    val chargerConnected: Boolean? = null,
    val hvPower: Double? = null,
    val obcPowerSinglePhase: Double? = null,
    val obcPowerThreePhase: Double? = null,
    val remoteTemperature: Double? = null,
    val heatedSeatFrontLeft: Int? = null,
    val heatedSeatFrontRight: Int? = null,
    val rearWindowDefroster: Boolean? = null,
    val steeringWheelHeating: Boolean? = null,
    val chargingType: String? = null,
    val chargingCableLock: Boolean? = null,
    /** In seconds, as the gateway reports it. */
    val remainingChargingTime: Double? = null,
    val batteryHeating: Boolean? = null,
    val socTargetPercent: Double? = null,
    val chargeCurrentLimit: String? = null,
    val chargingScheduleMode: String? = null,
    val chargingScheduleStartTime: String? = null,
    val chargingScheduleEndTime: String? = null,
    val lastVehicleStateAt: String? = null,
    /** Tyre pressures in bar, as the car last reported them. */
    val tyrePressureFrontLeft: Double? = null,
    val tyrePressureFrontRight: Double? = null,
    val tyrePressureRearLeft: Double? = null,
    val tyrePressureRearRight: Double? = null,
)

/** The server's tyre pressure limits, in bar. The defaults are the server's own. */
@Serializable
data class TyreLimits(
    val lowBar: Double = 2.2,
    val highBar: Double = 3.2,
    val recommendedBar: Double = 2.5,
)

@Serializable
data class Place(
    val road: String? = null,
    val houseNumber: String? = null,
    val city: String? = null,
    val displayName: String? = null,
)

/** One saved trip without its route (GET /api/vehicles/{vin}/trips/log). */
@Serializable
data class TripLogEntry(
    val id: Long,
    val startedAt: String,
    val endedAt: String,
    val distanceKm: Double = 0.0,
    val startLatitude: Double? = null,
    val startLongitude: Double? = null,
    val endLatitude: Double? = null,
    val endLongitude: Double? = null,
    val odometerStartKm: Double? = null,
    val odometerEndKm: Double? = null,
    val startPlace: Place? = null,
    val endPlace: Place? = null,
    val isHidden: Boolean = false,
    val avgMovingSpeedKmh: Double? = null,
    val maxSpeedKmh: Double? = null,
    val socStartPercent: Double? = null,
    val socEndPercent: Double? = null,
    val energyCounterUsed: Double? = null,
    /** Set on a drive saved without a route. */
    val leftAfter: String? = null,
    /** Set on a drive followed from part-way only. */
    val unfollowedKm: Double? = null,
)

/** A trip with its route packed as an encoded polyline (GET .../trips?format=compact). */
@Serializable
data class CompactTrip(
    val id: Long? = null,
    val startedAt: String,
    val endedAt: String,
    val line: String = "",
)

@Serializable
data class LoginRequest(val username: String, val password: String, val rememberMe: Boolean = true)

@Serializable
data class CommandRequest(val value: String)

/** The server's error body (RFC 7807 problem details, with the project's own code). */
@Serializable
data class Problem(val title: String? = null, val detail: String? = null, val code: String? = null)

/** The gateway's answer to a command, pushed over the live connection. */
@Serializable
data class CommandResult(val command: String, val success: Boolean, val detail: String? = null)
