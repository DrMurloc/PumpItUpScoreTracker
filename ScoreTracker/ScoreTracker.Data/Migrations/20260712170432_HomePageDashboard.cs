using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class HomePageDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomePage",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ordinal = table.Column<byte>(type: "tinyint", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    DefaultMixId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomePage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomePageWidget",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WidgetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Ordinal = table.Column<byte>(type: "tinyint", nullable: false),
                    SizePreset = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ConfigVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomePageWidget", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomePage_UserId",
                schema: "scores",
                table: "HomePage",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HomePageWidget_PageId",
                schema: "scores",
                table: "HomePageWidget",
                column: "PageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomePage",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "HomePageWidget",
                schema: "scores");
        }
    }
}
