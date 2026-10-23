using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTrip.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRecommendation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiRecommendation",
                table: "Trips",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiRecommendation",
                table: "Trips");
        }
    }
}
