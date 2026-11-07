using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageStack.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendChartIndex : Migration
    {
        // The index as RestoreChartIndex first created it, before the statistics read the odometer,
        // the temperatures and the electric range.
        private const string PreviousSql = """
            DROP INDEX IF EXISTS "IX_TelemetrySnapshots_VehicleId_RecordedAt_Chart";
            CREATE INDEX "IX_TelemetrySnapshots_VehicleId_RecordedAt_Chart"
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
                   OR "PowerUsageSinceLastCharge" IS NOT NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The statistics now also chart the outside and inside temperature, the electric range
            // and the distance driven per day, so the partial index covers those fields too.
            migrationBuilder.Sql(TelemetryChartIndex.RecreateSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreviousSql);
        }
    }
}
