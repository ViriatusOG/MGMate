using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripUnfollowedKm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "UnfollowedKm",
                table: "Trips",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnfollowedKm",
                table: "Trips");
        }
    }
}
