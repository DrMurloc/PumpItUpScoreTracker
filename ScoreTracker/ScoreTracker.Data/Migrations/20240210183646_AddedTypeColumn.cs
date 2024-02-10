using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class AddedTypeColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserWorldRanking_UserName",
                schema: "scores",
                table: "UserWorldRanking");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "scores",
                table: "UserWorldRanking",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorldRanking_UserName_Type",
                schema: "scores",
                table: "UserWorldRanking",
                columns: new[] { "UserName", "Type" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserWorldRanking_UserName_Type",
                schema: "scores",
                table: "UserWorldRanking");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "scores",
                table: "UserWorldRanking");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorldRanking_UserName",
                schema: "scores",
                table: "UserWorldRanking",
                column: "UserName");
        }
    }
}
