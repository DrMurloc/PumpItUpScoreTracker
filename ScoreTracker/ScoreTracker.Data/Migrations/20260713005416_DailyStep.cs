using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class DailyStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyStepChart",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsLimbo = table.Column<bool>(type: "bit", nullable: false),
                    ExpirationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStepChart", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyStepEntry",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsBroken = table.Column<bool>(type: "bit", nullable: false),
                    CompetitiveLevel = table.Column<double>(type: "float", nullable: false),
                    Photo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStepEntry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDailyStepPlacing",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsLimbo = table.Column<bool>(type: "bit", nullable: false),
                    Place = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsBroken = table.Column<bool>(type: "bit", nullable: false),
                    CompetitiveLevel = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyStepPlacing", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStepChart_MixId",
                schema: "scores",
                table: "DailyStepChart",
                column: "MixId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyStepEntry_UserId_ChartId_MixId",
                schema: "scores",
                table: "DailyStepEntry",
                columns: new[] { "UserId", "ChartId", "MixId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyStepPlacing_UserId_MixId",
                schema: "scores",
                table: "UserDailyStepPlacing",
                columns: new[] { "UserId", "MixId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyStepChart",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "DailyStepEntry",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "UserDailyStepPlacing",
                schema: "scores");
        }
    }
}
