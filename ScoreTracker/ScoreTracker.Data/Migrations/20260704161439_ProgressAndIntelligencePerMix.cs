using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgressAndIntelligencePerMix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTitle_UserId",
                schema: "scores",
                table: "UserTitle");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserHighestTitle",
                schema: "scores",
                table: "UserHighestTitle");

            migrationBuilder.DropIndex(
                name: "IX_TierListEntry_TierListName",
                schema: "scores",
                table: "TierListEntry");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerStats",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropIndex(
                name: "IX_PlayerHistory_UserId",
                schema: "scores",
                table: "PlayerHistory");

            // Backfill = the Phoenix mix row id (MixIds.Phoenix) on all six tables:
            // every pre-Phoenix-2 row is a Phoenix-mix record.
            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserTitle",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserHighestTitle",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "TierListEntry",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "PlayerStats",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "PlayerHistory",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "CoOpRating",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserHighestTitle",
                schema: "scores",
                table: "UserHighestTitle",
                columns: new[] { "UserId", "MixId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerStats",
                schema: "scores",
                table: "PlayerStats",
                columns: new[] { "UserId", "MixId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTitle_UserId_MixId",
                schema: "scores",
                table: "UserTitle",
                columns: new[] { "UserId", "MixId" });

            migrationBuilder.CreateIndex(
                name: "IX_TierListEntry_TierListName_MixId",
                schema: "scores",
                table: "TierListEntry",
                columns: new[] { "TierListName", "MixId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHistory_UserId_MixId",
                schema: "scores",
                table: "PlayerHistory",
                columns: new[] { "UserId", "MixId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTitle_UserId_MixId",
                schema: "scores",
                table: "UserTitle");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserHighestTitle",
                schema: "scores",
                table: "UserHighestTitle");

            migrationBuilder.DropIndex(
                name: "IX_TierListEntry_TierListName_MixId",
                schema: "scores",
                table: "TierListEntry");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerStats",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropIndex(
                name: "IX_PlayerHistory_UserId_MixId",
                schema: "scores",
                table: "PlayerHistory");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "UserTitle");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "UserHighestTitle");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "TierListEntry");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "PlayerHistory");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "CoOpRating");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserHighestTitle",
                schema: "scores",
                table: "UserHighestTitle",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerStats",
                schema: "scores",
                table: "PlayerStats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTitle_UserId",
                schema: "scores",
                table: "UserTitle",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TierListEntry_TierListName",
                schema: "scores",
                table: "TierListEntry",
                column: "TierListName");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHistory_UserId",
                schema: "scores",
                table: "PlayerHistory",
                column: "UserId");
        }
    }
}
