using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class ProfileExpansion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GameTag",
                schema: "scores",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImage",
                schema: "scores",
                table: "User",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameTag",
                schema: "scores",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ProfileImage",
                schema: "scores",
                table: "User");
        }
    }
}
