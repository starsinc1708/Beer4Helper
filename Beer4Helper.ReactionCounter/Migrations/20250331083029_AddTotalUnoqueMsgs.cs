using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beer4Helper.ReactionCounter.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalUnoqueMsgs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalUniqueMessages",
                table: "UserStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalUniqueMessages",
                table: "UserStats");
        }
    }
}
