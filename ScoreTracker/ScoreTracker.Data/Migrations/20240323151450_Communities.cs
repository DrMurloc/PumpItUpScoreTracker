using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class Communities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Community",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OwningUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrivacyType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Community", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityChannel",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    SendNewScores = table.Column<bool>(type: "bit", nullable: false),
                    SendTitles = table.Column<bool>(type: "bit", nullable: false),
                    SendNewMembers = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityChannel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityInviteCode",
                schema: "scores",
                columns: table => new
                {
                    InviteCode = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityInviteCode", x => x.InviteCode);
                });

            migrationBuilder.CreateTable(
                name: "CommunityMembership",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityMembership", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Community_Name",
                schema: "scores",
                table: "Community",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityChannel_CommunityId",
                schema: "scores",
                table: "CommunityChannel",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityInviteCode_CommunityId",
                schema: "scores",
                table: "CommunityInviteCode",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMembership_CommunityId_UserId",
                schema: "scores",
                table: "CommunityMembership",
                columns: new[] { "CommunityId", "UserId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Community",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "CommunityChannel",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "CommunityInviteCode",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "CommunityMembership",
                schema: "scores");
        }
    }
}
