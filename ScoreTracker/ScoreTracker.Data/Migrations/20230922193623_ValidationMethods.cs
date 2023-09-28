using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class ValidationMethods : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsApproval",
                schema: "scores",
                table: "UserTournamentSession",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationType",
                schema: "scores",
                table: "UserTournamentSession",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Unverified");

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                schema: "scores",
                table: "UserTournamentSession",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhotoVerification",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoVerification", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoVerification",
                schema: "scores");

            migrationBuilder.DropColumn(
                name: "NeedsApproval",
                schema: "scores",
                table: "UserTournamentSession");

            migrationBuilder.DropColumn(
                name: "VerificationType",
                schema: "scores",
                table: "UserTournamentSession");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                schema: "scores",
                table: "UserTournamentSession");
        }
    }
}
