using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTrip.Migrations
{
    /// <inheritdoc />
    public partial class AddTripDatesAndTravelers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Trips",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Trips",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Travelers",
                table: "Trips",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TripWeatherInfo",
                table: "Trips",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Travelers",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "TripWeatherInfo",
                table: "Trips");
        }
    }
}
