using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClimateScheduleSteeringWheelHeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SteeringWheelHeat",
                table: "ClimateSchedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SteeringWheelHeat",
                table: "ClimateSchedules");
        }
    }
}
