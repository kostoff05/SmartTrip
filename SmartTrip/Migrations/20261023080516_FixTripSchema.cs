using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTrip.Migrations
{
    /// <inheritdoc />
    public partial class FixTripSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WeatherInfo",
                table: "Trips",
                newName: "TravelTypes");

            migrationBuilder.RenameColumn(
                name: "PackingList",
                table: "Trips",
                newName: "TravelType");

            migrationBuilder.AddColumn<string>(
                name: "Interests",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Interests",
                table: "Trips");

            migrationBuilder.RenameColumn(
                name: "TravelTypes",
                table: "Trips",
                newName: "WeatherInfo");

            migrationBuilder.RenameColumn(
                name: "TravelType",
                table: "Trips",
                newName: "PackingList");
        }
    }
}
