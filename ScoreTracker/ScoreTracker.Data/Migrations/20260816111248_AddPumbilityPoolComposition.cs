using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPumbilityPoolComposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PumbilityPoolComposition",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BandKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Floor = table.Column<double>(type: "float", nullable: false),
                    Ceiling = table.Column<double>(type: "float", nullable: true),
                    Players = table.Column<int>(type: "int", nullable: false),
                    ChartsPooled = table.Column<int>(type: "int", nullable: false),
                    LevelSum = table.Column<double>(type: "float", nullable: false),
                    LevelPart = table.Column<double>(type: "float", nullable: false),
                    ScorePart = table.Column<double>(type: "float", nullable: false),
                    PlatePart = table.Column<double>(type: "float", nullable: false),
                    GradeCountsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PoolsCounted = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PumbilityPoolComposition", x => new { x.MixId, x.BandKey });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PumbilityPoolComposition",
                schema: "scores");
        }
    }
}
