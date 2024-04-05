using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class ImprovedTitle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "scores",
                table: "UserTitle",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserTitle_Title",
                schema: "scores",
                table: "UserTitle",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_UserTitle_UserId",
                schema: "scores",
                table: "UserTitle",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTitle_Title",
                schema: "scores",
                table: "UserTitle");

            migrationBuilder.DropIndex(
                name: "IX_UserTitle_UserId",
                schema: "scores",
                table: "UserTitle");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "scores",
                table: "UserTitle",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
