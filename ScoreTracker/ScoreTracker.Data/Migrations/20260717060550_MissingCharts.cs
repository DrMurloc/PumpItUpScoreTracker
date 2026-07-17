using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class MissingCharts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfficialMissingChart",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SongName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ChartType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: true),
                    FirstIdentified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastIdentified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialMissingChart", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialMissingChart_MixId_SongName_ChartType_Level",
                schema: "scores",
                table: "OfficialMissingChart",
                columns: new[] { "MixId", "SongName", "ChartType", "Level" },
                unique: true,
                filter: "[Level] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfficialMissingChart",
                schema: "scores");
        }
    }
}
