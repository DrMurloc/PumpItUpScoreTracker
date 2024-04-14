using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class TournamentExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TournamentId",
                schema: "scores",
                table: "UserQualifierHistory",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentId",
                schema: "scores",
                table: "UserQualifier",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("fa27b7fb-6ef4-481b-8eee-56fdcf58433c"));

            migrationBuilder.AddColumn<bool>(
                name: "IsHighlighted",
                schema: "scores",
                table: "Tournament",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LinkOverride",
                schema: "scores",
                table: "Tournament",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "scores",
                table: "Tournament",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Remote");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "scores",
                table: "Tournament",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Stamina");

            migrationBuilder.CreateTable(
                name: "QualifiersConfiguration",
                schema: "scores",
                columns: table => new
                {
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoringType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Charts = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualifiersConfiguration", x => x.TournamentId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserQualifier_TournamentId",
                schema: "scores",
                table: "UserQualifier",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualifiersConfiguration",
                schema: "scores");

            migrationBuilder.DropIndex(
                name: "IX_UserQualifier_TournamentId",
                schema: "scores",
                table: "UserQualifier");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                schema: "scores",
                table: "UserQualifierHistory");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                schema: "scores",
                table: "UserQualifier");

            migrationBuilder.DropColumn(
                name: "IsHighlighted",
                schema: "scores",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "LinkOverride",
                schema: "scores",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "scores",
                table: "Tournament");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "scores",
                table: "Tournament");
        }
    }
}
