using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     What a tool must carry before it touches anyone else's scores: a public source repository
    ///     players can read, and a Discord handle its maker can be reached on. Plus the timestamp of
    ///     the maker accepting the rules.
    ///     <para>
    ///         All five are nullable, and deliberately so. A maker building against their own scores
    ///         needs none of them, and PIU Tracker arrived Public with 653 migrated players before
    ///         the requirement existed. The rule is enforced by <c>Tool.CanBeSharedWithOthers</c>,
    ///         which is a gate on reaching a second player rather than a constraint on the row —
    ///         a NOT NULL column here would have blocked the seeded tool outright.
    ///     </para>
    /// </summary>
    public partial class ToolSourceAndMaker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AgreedToRulesAt",
                schema: "scores",
                table: "Tool",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordHandle",
                schema: "scores",
                table: "Tool",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RepositoryCheckedAt",
                schema: "scores",
                table: "Tool",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepositoryOwner",
                schema: "scores",
                table: "Tool",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepositoryUrl",
                schema: "scores",
                table: "Tool",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreedToRulesAt",
                schema: "scores",
                table: "Tool");

            migrationBuilder.DropColumn(
                name: "DiscordHandle",
                schema: "scores",
                table: "Tool");

            migrationBuilder.DropColumn(
                name: "RepositoryCheckedAt",
                schema: "scores",
                table: "Tool");

            migrationBuilder.DropColumn(
                name: "RepositoryOwner",
                schema: "scores",
                table: "Tool");

            migrationBuilder.DropColumn(
                name: "RepositoryUrl",
                schema: "scores",
                table: "Tool");
        }
    }
}
