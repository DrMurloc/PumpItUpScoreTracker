using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PhoenixScoreStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhoenixRecordStats",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pumbility = table.Column<double>(type: "float", nullable: false),
                    PumbilityPlus = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoenixRecordStats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecordStats_UserId_ChartId",
                schema: "scores",
                table: "PhoenixRecordStats",
                columns: new[] { "UserId", "ChartId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhoenixRecordStats",
                schema: "scores");
        }
    }
}
