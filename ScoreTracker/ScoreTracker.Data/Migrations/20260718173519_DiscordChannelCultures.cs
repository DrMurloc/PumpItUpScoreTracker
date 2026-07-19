using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiscordChannelCultures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Culture",
                schema: "scores",
                table: "DiscordFeedSubscription",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Culture",
                schema: "scores",
                table: "CommunityChannel",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Culture",
                schema: "scores",
                table: "DiscordFeedSubscription");

            migrationBuilder.DropColumn(
                name: "Culture",
                schema: "scores",
                table: "CommunityChannel");
        }
    }
}
