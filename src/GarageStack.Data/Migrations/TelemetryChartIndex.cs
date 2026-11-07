namespace GarageStack.Data.Migrations;

/// <summary>
/// The partial index the statistics read through (GetDailyStatsAsync): only rows carrying one of
/// TelemetryHistoryPoint's fields. It is raw SQL, so EF's model does not know it, which is how a
/// scaffolded migration once dropped it (AddVehicleLastParkedAt). ChartIndexTests keeps its field
/// list in step with the record; a change to it needs a migration that runs RecreateSql
/// (as ExtendChartIndex does), since an existing index is left as it is by CreateIfMissingSql.
/// </summary>
public static class TelemetryChartIndex
{
    public const string CreateIfMissingSql = """
        CREATE INDEX IF NOT EXISTS "IX_TelemetrySnapshots_VehicleId_RecordedAt_Chart"
            ON "TelemetrySnapshots" ("VehicleId", "RecordedAt")
            WHERE "FuelLevelPercent"          IS NOT NULL
               OR "EvSocPercent"              IS NOT NULL
               OR "PowerUsageOfDay"           IS NOT NULL
               OR "BatteryVoltage"            IS NOT NULL
               OR "ClimateOn"                 IS NOT NULL
               OR "IsCharging"                IS NOT NULL
               OR "TyrePressureFrontLeft"     IS NOT NULL
               OR "TyrePressureFrontRight"    IS NOT NULL
               OR "TyrePressureRearLeft"      IS NOT NULL
               OR "TyrePressureRearRight"     IS NOT NULL
               OR "MileageOfTheDay"           IS NOT NULL
               OR "MileageSinceLastCharge"    IS NOT NULL
               OR "HvSocKwh"                  IS NOT NULL
               OR "HvTotalCapacityKwh"        IS NOT NULL
               OR "PowerUsageSinceLastCharge" IS NOT NULL
               OR "OdometerKm"                IS NOT NULL
               OR "InteriorTemperature"       IS NOT NULL
               OR "ExteriorTemperature"       IS NOT NULL
               OR "ElectricRangeKm"           IS NOT NULL;
        """;

    /// <summary>Replaces the index whatever field list it was created with.</summary>
    public const string RecreateSql =
        """DROP INDEX IF EXISTS "IX_TelemetrySnapshots_VehicleId_RecordedAt_Chart";""" + "\n" + CreateIfMissingSql;
}
