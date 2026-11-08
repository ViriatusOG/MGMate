using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MGMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChargeSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ChargesRecordedUntil",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChargeSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SocStartPercent = table.Column<double>(type: "double precision", nullable: true),
                    SocEndPercent = table.Column<double>(type: "double precision", nullable: true),
                    EnergyAddedKwh = table.Column<double>(type: "double precision", nullable: true),
                    MaxPowerKw = table.Column<double>(type: "double precision", nullable: true),
                    ChargeType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    OdometerKm = table.Column<double>(type: "double precision", nullable: true),
                    StopReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SocTargetPercent = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Place = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    PaidAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargeSessions_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargeSessions_VehicleId_StartedAt",
                table: "ChargeSessions",
                columns: new[] { "VehicleId", "StartedAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargeSessions");

            migrationBuilder.DropColumn(
                name: "ChargesRecordedUntil",
                table: "Vehicles");
        }
    }
}
