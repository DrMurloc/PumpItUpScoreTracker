using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChartLetterDifficulties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartLetterDifficulty",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetterGrade = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Percentile = table.Column<double>(type: "float", nullable: false),
                    WeightedSum = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartLetterDifficulty", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartLetterDifficulty_ChartId_LetterGrade",
                schema: "scores",
                table: "ChartLetterDifficulty",
                columns: new[] { "ChartId", "LetterGrade" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartLetterDifficulty",
                schema: "scores");
        }
    }
}
