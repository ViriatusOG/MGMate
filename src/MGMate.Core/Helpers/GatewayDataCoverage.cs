using System.Collections.Frozen;

namespace MGMate.Core.Helpers;

/// <summary>What MGMate keeps of one gateway value.</summary>
public enum GatewayDataKept
{
    /// <summary>Stored on every poll's telemetry row, so it has a history to chart.</summary>
    History,

    /// <summary>Only the newest value is stored, on the vehicle record.</summary>
    Latest,

    /// <summary>Shown live, never stored.</summary>
    NotKept,
}

/// <summary>
/// Which of the gateway's vehicle subtopics MGMate stores, and how. The Worker's telemetry mapper
/// and its latest-value handler store exactly these (a test holds the mapper to the list), and the
/// Debug page marks each value with it.
/// </summary>
public static class GatewayDataCoverage
{
    /// <summary>Subtopics the Worker writes onto the telemetry row of each poll.</summary>
    public static readonly FrozenSet<string> TelemetrySubtopics = new[]
    {
        "12v/batteryVoltage", "available", "battery/voltage", "bms/chargeStatus",
        "ccu/offboardChargerPlugStatus", "ccu/onboardChargerPlugStatus",
        "climate/active", "climate/exteriorTemperature", "climate/heatedSeatsFrontLeftLevel",
        "climate/heatedSeatsFrontRightLevel", "climate/interiorTemperature", "climate/on",
        "climate/rearWindowDefrosterHeating", "climate/remoteClimateState", "climate/remoteTemperature", "climate/steeringWheelHeating",
        "doors/bonnet", "doors/boot", "doors/driver", "doors/hood", "doors/locked", "doors/passenger",
        "doors/rearLeft", "doors/rearRight", "doors/trunk",
        "drivetrain/auxiliaryBatteryVoltage", "drivetrain/batteryHeating", "drivetrain/batteryHeatingSchedule",
        "drivetrain/batteryHeatingSchedule/mode", "drivetrain/batteryHeatingSchedule/startTime",
        "drivetrain/batteryHeatingStopReason",
        "drivetrain/chargeCurrentLimit", "drivetrain/chargerConnected", "drivetrain/charging",
        "drivetrain/charging/lastEnd", "drivetrain/charging/lastStart", "drivetrain/chargingCableLock",
        "drivetrain/chargingSchedule", "drivetrain/chargingStopReason", "drivetrain/chargingType",
        "drivetrain/current", "drivetrain/currentJourney", "drivetrain/currentJourney/distance",
        "drivetrain/fossilFuel/percentage", "drivetrain/fossilFuel/range", "drivetrain/fuelLevel",
        "drivetrain/fuelLevelPercent", "drivetrain/fuelRange", "drivetrain/hvBatteryActive",
        "drivetrain/lastChargeEndingPower", "drivetrain/mileage", "drivetrain/mileageOfTheDay",
        "drivetrain/mileageSinceLastCharge", "drivetrain/odometer", "drivetrain/power",
        "drivetrain/powerUsageOfDay", "drivetrain/powerUsageSinceLastCharge", "drivetrain/range",
        "drivetrain/remainingChargingTime", "drivetrain/running", "drivetrain/soc", "drivetrain/socTarget",
        "drivetrain/soc_kwh", "drivetrain/speed", "drivetrain/totalBatteryCapacity", "drivetrain/voltage",
        "lights/dippedBeam", "lights/mainBeam", "lights/side",
        "location/elevation", "location/heading", "location/latitude", "location/longitude",
        "location/position", "location/speed",
        "obc/current", "obc/powerSinglePhase", "obc/powerThreePhase", "obc/voltage",
        "refresh/lastActivity", "refresh/lastCachedState", "refresh/lastChargeState",
        "refresh/lastStatusTime", "refresh/lastVehicleState",
        "tyres/frontLeftPressure", "tyres/frontRightPressure", "tyres/rearLeftPressure", "tyres/rearRightPressure",
        "windows/driver", "windows/passenger", "windows/rearLeft", "windows/rearRight", "windows/sunRoof",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Subtopics with no use as a history, whose newest value the Worker keeps on the vehicle
    /// record under <see cref="LatestValueKeyPrefix"/>: the gateway's own state and the car's
    /// fixed details. (command/error is not one: it repeats the failure the command's /result
    /// topic already carried, which reaches the browser as the command's answer.)
    /// </summary>
    public static readonly FrozenSet<string> LatestValueSubtopics = new[]
    {
        "refresh/lastError", "refresh/pollingPhase",
        "info/brand", "info/model", "info/year", "info/series", "info/color",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The key prefix of a latest value in the vehicle's config.</summary>
    public const string LatestValueKeyPrefix = "gateway:";

    // Capability flags, stored as the vehicle's config (VehicleConfigHandler).
    private const string ConfigurationPrefix = "info/configuration/";

    public static GatewayDataKept Classify(string subtopic) =>
        TelemetrySubtopics.Contains(subtopic) ? GatewayDataKept.History
        : LatestValueSubtopics.Contains(subtopic) || subtopic.StartsWith(ConfigurationPrefix, StringComparison.Ordinal)
            ? GatewayDataKept.Latest
            : GatewayDataKept.NotKept;
}
