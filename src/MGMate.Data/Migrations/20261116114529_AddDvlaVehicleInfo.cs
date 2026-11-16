using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MGMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDvlaVehicleInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DvlaVehicleInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    LicensePlate = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Found = table.Column<bool>(type: "boolean", nullable: false),
                    TaxStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TaxDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MotStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MotExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Make = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Colour = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FuelType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RecordJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DvlaVehicleInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DvlaVehicleInfos_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DvlaVehicleInfos_VehicleId",
                table: "DvlaVehicleInfos",
                column: "VehicleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DvlaVehicleInfos");
        }
    }
}
