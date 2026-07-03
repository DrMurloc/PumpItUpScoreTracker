using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MergeRequest",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurvivorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetiredUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovedLogins = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetiredUserSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PurgeAfter = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PurgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeRequest", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MergeRequest_RetiredUserId",
                schema: "scores",
                table: "MergeRequest",
                column: "RetiredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MergeRequest_State_PurgeAfter",
                schema: "scores",
                table: "MergeRequest",
                columns: new[] { "State", "PurgeAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_MergeRequest_SurvivorUserId",
                schema: "scores",
                table: "MergeRequest",
                column: "SurvivorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MergeRequest",
                schema: "scores");
        }
    }
}
