using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class SongLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SongNameLanguage",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnglishSongName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CultureCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    SongName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongNameLanguage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SongNameLanguage_CultureCode",
                schema: "scores",
                table: "SongNameLanguage",
                column: "CultureCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SongNameLanguage",
                schema: "scores");
        }
    }
}
