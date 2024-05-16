using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class TournamentIdForBrackets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RandomSettings_Name",
                schema: "scores",
                table: "RandomSettings");

            migrationBuilder.DropIndex(
                name: "IX_MatchLink_FromMatch",
                schema: "scores",
                table: "MatchLink");

            migrationBuilder.DropIndex(
                name: "IX_MatchLink_ToMatch",
                schema: "scores",
                table: "MatchLink");

            migrationBuilder.DropIndex(
                name: "IX_Match_Name",
                schema: "scores",
                table: "Match");

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentId",
                schema: "scores",
                table: "RandomSettings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentId",
                schema: "scores",
                table: "MatchLink",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentId",
                schema: "scores",
                table: "Match",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));

            migrationBuilder.CreateIndex(
                name: "IX_RandomSettings_TournamentId_Name",
                schema: "scores",
                table: "RandomSettings",
                columns: new[] { "TournamentId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchLink_TournamentId_FromMatch_ToMatch",
                schema: "scores",
                table: "MatchLink",
                columns: new[] { "TournamentId", "FromMatch", "ToMatch" });

            migrationBuilder.CreateIndex(
                name: "IX_Match_TournamentId_Name",
                schema: "scores",
                table: "Match",
                columns: new[] { "TournamentId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RandomSettings_TournamentId_Name",
                schema: "scores",
                table: "RandomSettings");

            migrationBuilder.DropIndex(
                name: "IX_MatchLink_TournamentId_FromMatch_ToMatch",
                schema: "scores",
                table: "MatchLink");

            migrationBuilder.DropIndex(
                name: "IX_Match_TournamentId_Name",
                schema: "scores",
                table: "Match");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                schema: "scores",
                table: "RandomSettings");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                schema: "scores",
                table: "MatchLink");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                schema: "scores",
                table: "Match");

            migrationBuilder.CreateIndex(
                name: "IX_RandomSettings_Name",
                schema: "scores",
                table: "RandomSettings",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLink_FromMatch",
                schema: "scores",
                table: "MatchLink",
                column: "FromMatch");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLink_ToMatch",
                schema: "scores",
                table: "MatchLink",
                column: "ToMatch");

            migrationBuilder.CreateIndex(
                name: "IX_Match_Name",
                schema: "scores",
                table: "Match",
                column: "Name");
        }
    }
}
