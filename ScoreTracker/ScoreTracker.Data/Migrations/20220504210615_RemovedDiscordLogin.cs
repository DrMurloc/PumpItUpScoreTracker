using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class RemovedDiscordLogin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordLogin",
                schema: "scores");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
