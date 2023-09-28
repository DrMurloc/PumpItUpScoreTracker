using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class FirstMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scores");

            migrationBuilder.CreateTable(
                name: "Song",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Song", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chart",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SongId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chart", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chart_Song_SongId",
                        column: x => x.SongId,
                        principalSchema: "scores",
                        principalTable: "Song",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BestAttempt",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LetterGrade = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBroken = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BestAttempt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BestAttempt_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BestAttempt_ChartId",
                schema: "scores",
                table: "BestAttempt",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_BestAttempt_UserId_ChartId",
                schema: "scores",
                table: "BestAttempt",
                columns: new[] { "UserId", "ChartId" });

            migrationBuilder.CreateIndex(
                name: "IX_Chart_Level",
                schema: "scores",
                table: "Chart",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Chart_SongId",
                schema: "scores",
                table: "Chart",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_Chart_Type",
                schema: "scores",
                table: "Chart",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Song_Name",
                schema: "scores",
                table: "Song",
                column: "Name");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BestAttempt",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "Chart",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "Song",
                schema: "scores");
        }
    }
}
