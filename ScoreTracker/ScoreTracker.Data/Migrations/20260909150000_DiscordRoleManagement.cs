using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiscordRoleManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityDiscordGrant",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    LastReconciledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityDiscordGrant", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityDiscordServer",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuildId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    GuildName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DesignatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityDiscordServer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityTitleRole",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RoleId = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityTitleRole", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDiscordGrant_CommunityId_UserId",
                schema: "scores",
                table: "CommunityDiscordGrant",
                columns: new[] { "CommunityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDiscordGrant_UserId",
                schema: "scores",
                table: "CommunityDiscordGrant",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDiscordServer_CommunityId",
                schema: "scores",
                table: "CommunityDiscordServer",
                column: "CommunityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityTitleRole_CommunityId_RoleId",
                schema: "scores",
                table: "CommunityTitleRole",
                columns: new[] { "CommunityId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityTitleRole_CommunityId_TitleName",
                schema: "scores",
                table: "CommunityTitleRole",
                columns: new[] { "CommunityId", "TitleName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityDiscordGrant",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "CommunityDiscordServer",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "CommunityTitleRole",
                schema: "scores");
        }
    }
}
