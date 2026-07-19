using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiscordFeedSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SendNewMembers",
                schema: "scores",
                table: "CommunityChannel");

            migrationBuilder.DropColumn(
                name: "SendNewScores",
                schema: "scores",
                table: "CommunityChannel");

            migrationBuilder.DropColumn(
                name: "SendTitles",
                schema: "scores",
                table: "CommunityChannel");

            migrationBuilder.CreateTable(
                name: "DiscordFeedSubscription",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    FeedKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Mix = table.Column<int>(type: "int", nullable: false),
                    RegisteredByDiscordUserId = table.Column<decimal>(type: "decimal(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordFeedSubscription", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscordFeedSubscription_ChannelId_FeedKind_Mix",
                schema: "scores",
                table: "DiscordFeedSubscription",
                columns: new[] { "ChannelId", "FeedKind", "Mix" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordFeedSubscription",
                schema: "scores");

            migrationBuilder.AddColumn<bool>(
                name: "SendNewMembers",
                schema: "scores",
                table: "CommunityChannel",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SendNewScores",
                schema: "scores",
                table: "CommunityChannel",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SendTitles",
                schema: "scores",
                table: "CommunityChannel",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
