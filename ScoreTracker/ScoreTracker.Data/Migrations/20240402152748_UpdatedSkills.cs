using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class UpdatedSkills : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Skill",
                schema: "scores");

            migrationBuilder.DropColumn(
                name: "SkillId",
                schema: "scores",
                table: "ChartSkill");

            migrationBuilder.AddColumn<bool>(
                name: "IsHighlighted",
                schema: "scores",
                table: "ChartSkill",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkillName",
                schema: "scores",
                table: "ChartSkill",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ChartSkill_SkillName",
                schema: "scores",
                table: "ChartSkill",
                column: "SkillName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChartSkill_SkillName",
                schema: "scores",
                table: "ChartSkill");

            migrationBuilder.DropColumn(
                name: "IsHighlighted",
                schema: "scores",
                table: "ChartSkill");

            migrationBuilder.DropColumn(
                name: "SkillName",
                schema: "scores",
                table: "ChartSkill");

            migrationBuilder.AddColumn<Guid>(
                name: "SkillId",
                schema: "scores",
                table: "ChartSkill",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Skill",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skill", x => x.Id);
                });
        }
    }
}
