using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class AddUserTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "User",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscordLogin",
                schema: "scores",
                columns: table => new
                {
                    DiscordId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordLogin", x => x.DiscordId);
                    table.ForeignKey(
                        name: "FK_DiscordLogin_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "scores",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscordLogin_UserId",
                schema: "scores",
                table: "DiscordLogin",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BestAttempt_User_UserId",
                schema: "scores",
                table: "BestAttempt",
                column: "UserId",
                principalSchema: "scores",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BestAttempt_User_UserId",
                schema: "scores",
                table: "BestAttempt");

            migrationBuilder.DropTable(
                name: "DiscordLogin",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "User",
                schema: "scores");
        }
    }
}
