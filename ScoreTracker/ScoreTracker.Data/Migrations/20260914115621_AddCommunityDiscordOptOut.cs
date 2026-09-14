using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityDiscordOptOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityDiscordOptOut",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptedOutAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityDiscordOptOut", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDiscordOptOut_CommunityId_UserId",
                schema: "scores",
                table: "CommunityDiscordOptOut",
                columns: new[] { "CommunityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDiscordOptOut_UserId",
                schema: "scores",
                table: "CommunityDiscordOptOut",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityDiscordOptOut",
                schema: "scores");
        }
    }
}
