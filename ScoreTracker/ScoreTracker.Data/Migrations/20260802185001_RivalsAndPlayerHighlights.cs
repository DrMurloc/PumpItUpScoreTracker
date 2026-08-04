using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class RivalsAndPlayerHighlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerHighlight",
                schema: "scores",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerHighlight", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Rival",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetTag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rival", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RivalBlock",
                schema: "scores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RivalBlock", x => new { x.UserId, x.BlockedUserId });
                });

            migrationBuilder.CreateTable(
                name: "RivalInviteCode",
                schema: "scores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RivalInviteCode", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHighlight_OccurredAt",
                schema: "scores",
                table: "PlayerHighlight",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHighlight_UserId_MixId_OccurredAt",
                schema: "scores",
                table: "PlayerHighlight",
                columns: new[] { "UserId", "MixId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Rival_OwnerUserId_TargetTag",
                schema: "scores",
                table: "Rival",
                columns: new[] { "OwnerUserId", "TargetTag" },
                unique: true,
                filter: "[TargetTag] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rival_OwnerUserId_TargetUserId",
                schema: "scores",
                table: "Rival",
                columns: new[] { "OwnerUserId", "TargetUserId" },
                unique: true,
                filter: "[TargetUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rival_TargetTag",
                schema: "scores",
                table: "Rival",
                column: "TargetTag");

            migrationBuilder.CreateIndex(
                name: "IX_Rival_TargetUserId",
                schema: "scores",
                table: "Rival",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RivalBlock_BlockedUserId",
                schema: "scores",
                table: "RivalBlock",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RivalInviteCode_Code",
                schema: "scores",
                table: "RivalInviteCode",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerHighlight",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "Rival",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "RivalBlock",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "RivalInviteCode",
                schema: "scores");
        }
    }
}
