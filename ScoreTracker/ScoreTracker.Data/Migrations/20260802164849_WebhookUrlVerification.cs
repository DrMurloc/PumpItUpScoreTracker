using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     A webhook URL must be proven before anything is sent to it: we POST a challenge, the
    ///     endpoint echoes it back, and only then does <c>WebhookUrlVerifiedAt</c> get a value.
    ///     Required for every mode (owner, 2026-08-02) — an unverified URL means we would send a
    ///     player's scores, or in session mode their piugame credential, to a host nobody proved
    ///     they own.
    ///     <para>
    ///         Null for every existing row, which is correct: nothing in production has a webhook yet,
    ///         so there is nothing to grandfather. The one exception is PIU Tracker, whose endpoint we
    ///         have been posting to for years — re-proving it on deploy day would break 653 players'
    ///         sync while TUSA reads an email.
    ///     </para>
    /// </summary>
    public partial class WebhookUrlVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WebhookUrlVerifiedAt",
                schema: "scores",
                table: "Tool",
                type: "datetimeoffset",
                nullable: true);

            // PIU Tracker. Seeded verified for the reason in the summary above; the id matches
            // SeedPiuTrackerTool and PiuTrackerSessionShape.ToolId.
            migrationBuilder.Sql(@"
UPDATE scores.Tool
SET WebhookUrlVerifiedAt = SYSDATETIMEOFFSET()
WHERE Id = '7B1B7F8E-6F1E-4C4B-9F3E-2C0D5A9E4B10';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebhookUrlVerifiedAt",
                schema: "scores",
                table: "Tool");
        }
    }
}
