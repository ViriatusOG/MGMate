using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGatewayStatusTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatteryHeatingStopReason",
                table: "TelemetrySnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "TelemetrySnapshots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCachedStateAt",
                table: "TelemetrySnapshots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusAt",
                table: "TelemetrySnapshots",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryHeatingStopReason",
                table: "TelemetrySnapshots");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "TelemetrySnapshots");

            migrationBuilder.DropColumn(
                name: "LastCachedStateAt",
                table: "TelemetrySnapshots");

            migrationBuilder.DropColumn(
                name: "LastStatusAt",
                table: "TelemetrySnapshots");
        }
    }
}
