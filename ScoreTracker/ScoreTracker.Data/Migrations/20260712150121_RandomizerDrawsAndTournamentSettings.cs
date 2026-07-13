using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class RandomizerDrawsAndTournamentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mix",
                schema: "scores",
                table: "UserRandomSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Phoenix");

            migrationBuilder.AddColumn<Guid>(
                name: "ShareToken",
                schema: "scores",
                table: "UserRandomSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RandomizerDraw",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Slug = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mix = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RandomizerDraw", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TournamentRandomSettings",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mix = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentRandomSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RandomizerDrawCard",
                schema: "scores",
                columns: table => new
                {
                    PullId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrawId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RandomizerDrawCard", x => x.PullId);
                    table.ForeignKey(
                        name: "FK_RandomizerDrawCard_RandomizerDraw_DrawId",
                        column: x => x.DrawId,
                        principalSchema: "scores",
                        principalTable: "RandomizerDraw",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRandomSettings_ShareToken",
                schema: "scores",
                table: "UserRandomSettings",
                column: "ShareToken",
                unique: true,
                filter: "[ShareToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RandomizerDraw_Slug",
                schema: "scores",
                table: "RandomizerDraw",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RandomizerDraw_TournamentId",
                schema: "scores",
                table: "RandomizerDraw",
                column: "TournamentId",
                unique: true,
                filter: "[TournamentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RandomizerDraw_UserId",
                schema: "scores",
                table: "RandomizerDraw",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RandomizerDrawCard_DrawId",
                schema: "scores",
                table: "RandomizerDrawCard",
                column: "DrawId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRandomSettings_TournamentId_Name",
                schema: "scores",
                table: "TournamentRandomSettings",
                columns: new[] { "TournamentId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RandomizerDrawCard",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "TournamentRandomSettings",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "RandomizerDraw",
                schema: "scores");

            migrationBuilder.DropIndex(
                name: "IX_UserRandomSettings_ShareToken",
                schema: "scores",
                table: "UserRandomSettings");

            migrationBuilder.DropColumn(
                name: "Mix",
                schema: "scores",
                table: "UserRandomSettings");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                schema: "scores",
                table: "UserRandomSettings");
        }
    }
}
