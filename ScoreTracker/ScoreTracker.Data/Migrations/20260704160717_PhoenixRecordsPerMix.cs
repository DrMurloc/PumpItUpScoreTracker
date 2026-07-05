using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PhoenixRecordsPerMix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecordStats_UserId_ChartId",
                schema: "scores",
                table: "PhoenixRecordStats");

            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_UserId_ChartId",
                schema: "scores",
                table: "PhoenixRecord");

            // Backfill = the Phoenix mix row id (MixIds.Phoenix): every pre-Phoenix-2 row
            // in these tables is a Phoenix-mix record.
            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "PhoenixRecordStats",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "PhoenixRecord",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecordStats_UserId_ChartId_MixId",
                schema: "scores",
                table: "PhoenixRecordStats",
                columns: new[] { "UserId", "ChartId", "MixId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_UserId_ChartId_MixId",
                schema: "scores",
                table: "PhoenixRecord",
                columns: new[] { "UserId", "ChartId", "MixId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecordStats_UserId_ChartId_MixId",
                schema: "scores",
                table: "PhoenixRecordStats");

            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_UserId_ChartId_MixId",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "PhoenixRecordStats");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecordStats_UserId_ChartId",
                schema: "scores",
                table: "PhoenixRecordStats",
                columns: new[] { "UserId", "ChartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_UserId_ChartId",
                schema: "scores",
                table: "PhoenixRecord",
                columns: new[] { "UserId", "ChartId" },
                unique: true);
        }
    }
}
