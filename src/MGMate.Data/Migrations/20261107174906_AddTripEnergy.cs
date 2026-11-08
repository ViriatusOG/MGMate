using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripEnergy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EnergyCounterUsed",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnergyRead",
                table: "Trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "SocEndPercent",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SocStartPercent",
                table: "Trips",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnergyCounterUsed",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EnergyRead",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "SocEndPercent",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "SocStartPercent",
                table: "Trips");
        }
    }
}
