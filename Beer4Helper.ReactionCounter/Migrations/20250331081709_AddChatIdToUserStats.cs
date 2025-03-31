using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beer4Helper.ReactionCounter.Migrations
{
    /// <inheritdoc />
    public partial class AddChatIdToUserStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ChatId",
                table: "UserStats",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "UserStats");
        }
    }
}
