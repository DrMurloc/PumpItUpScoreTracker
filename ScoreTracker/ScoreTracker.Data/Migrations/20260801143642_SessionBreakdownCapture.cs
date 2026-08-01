using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionBreakdownCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptsBeforeClear",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OfficialAsOf",
                schema: "scores",
                table: "ScoreHighlight",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfficialBoardDepth",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfficialPlace",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PeerPercentile",
                schema: "scores",
                table: "ScoreHighlight",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDoublesPumbilityRank",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedPumbilityRank",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedSinglesPumbilityRank",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PumbilityBoardAsOf",
                schema: "scores",
                table: "PlayerStats",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptsBeforeClear",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "OfficialAsOf",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "OfficialBoardDepth",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "OfficialPlace",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "PeerPercentile",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "EstimatedDoublesPumbilityRank",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "EstimatedPumbilityRank",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "EstimatedSinglesPumbilityRank",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "PumbilityBoardAsOf",
                schema: "scores",
                table: "PlayerStats");
        }
    }
}
