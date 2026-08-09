using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportRestartRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessedAt",
                schema: "scores",
                table: "ScoreSession",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcknowledgedAt",
                schema: "scores",
                table: "ImportResult",
                type: "datetimeoffset",
                nullable: true);

            // Every session that predates the marker is processed by definition — its derived work
            // either ran or was lost long before anything existed to recover it. Without this, the
            // first boot after deploy sees the whole history as unprocessed and tries to replay
            // all of it: "unprocessed" must never be able to mean "older than the feature"
            // (docs/design/import-restart-recovery.md §4.1).
            //
            // Runs BEFORE the filtered index below on purpose, so the index is built against the
            // handful of rows that survive the backfill rather than every session ever recorded.
            migrationBuilder.Sql(
                "UPDATE scores.ScoreSession SET ProcessedAt = LastActivityAt WHERE ProcessedAt IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreSession_ProcessedAt",
                schema: "scores",
                table: "ScoreSession",
                column: "ProcessedAt",
                filter: "[ProcessedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ImportResult_SessionId",
                schema: "scores",
                table: "ImportResult",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScoreSession_ProcessedAt",
                schema: "scores",
                table: "ScoreSession");

            migrationBuilder.DropIndex(
                name: "IX_ImportResult_SessionId",
                schema: "scores",
                table: "ImportResult");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                schema: "scores",
                table: "ScoreSession");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                schema: "scores",
                table: "ImportResult");
        }
    }
}
