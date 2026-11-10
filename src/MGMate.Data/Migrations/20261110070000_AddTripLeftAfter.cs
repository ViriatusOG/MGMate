using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripLeftAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LeftAfter",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeftAfter",
                table: "Trips");
        }
    }
}
