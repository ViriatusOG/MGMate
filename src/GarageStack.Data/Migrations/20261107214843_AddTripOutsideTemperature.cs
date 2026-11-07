using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageStack.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripOutsideTemperature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OutsideTemperature",
                table: "Trips",
                type: "double precision",
                nullable: true);

            // Every saved trip reads its energy again, which now carries the outside temperature;
            // housekeeping works through them in batches.
            migrationBuilder.Sql("""UPDATE "Trips" SET "EnergyRead" = false;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OutsideTemperature",
                table: "Trips");
        }
    }
}
