using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageStack.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChargerEnergy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ChargerEnergyKwh",
                table: "ChargeSessions",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargerEnergyKwh",
                table: "ChargeSessions");
        }
    }
}
