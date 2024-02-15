using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class ChartMetaData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Artist",
                schema: "scores",
                table: "Song",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxBpm",
                schema: "scores",
                table: "Song",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinBpm",
                schema: "scores",
                table: "Song",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepArtist",
                schema: "scores",
                table: "Chart",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChartSkill",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartSkill", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skill",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skill", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartSkill_ChartId",
                schema: "scores",
                table: "ChartSkill",
                column: "ChartId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartSkill",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "Skill",
                schema: "scores");

            migrationBuilder.DropColumn(
                name: "Artist",
                schema: "scores",
                table: "Song");

            migrationBuilder.DropColumn(
                name: "MaxBpm",
                schema: "scores",
                table: "Song");

            migrationBuilder.DropColumn(
                name: "MinBpm",
                schema: "scores",
                table: "Song");

            migrationBuilder.DropColumn(
                name: "StepArtist",
                schema: "scores",
                table: "Chart");
        }
    }
}
