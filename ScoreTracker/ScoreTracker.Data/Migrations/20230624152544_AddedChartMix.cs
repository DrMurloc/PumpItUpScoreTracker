using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class AddedChartMix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mix",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mix", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartMix",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartMix", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChartMix_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChartMix_Mix_MixId",
                        column: x => x.MixId,
                        principalSchema: "scores",
                        principalTable: "Mix",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartMix_ChartId",
                schema: "scores",
                table: "ChartMix",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartMix_Level",
                schema: "scores",
                table: "ChartMix",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_ChartMix_MixId",
                schema: "scores",
                table: "ChartMix",
                column: "MixId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartMix",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "Mix",
                schema: "scores");
        }
    }
}
