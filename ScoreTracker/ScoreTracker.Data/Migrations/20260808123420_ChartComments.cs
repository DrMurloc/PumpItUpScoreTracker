using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChartComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartComment",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartComment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartCommentConsent",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreedToTermsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TermsVersion = table.Column<int>(type: "int", nullable: false),
                    ConsentedToPublicIdentityAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartCommentConsent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartCommentRevision",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReplacedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartCommentRevision", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartCommentVote",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartCommentVote", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartComment_ChartId_Audience_CommunityId",
                schema: "scores",
                table: "ChartComment",
                columns: new[] { "ChartId", "Audience", "CommunityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChartComment_ParentCommentId",
                schema: "scores",
                table: "ChartComment",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartComment_UserId",
                schema: "scores",
                table: "ChartComment",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentConsent_UserId",
                schema: "scores",
                table: "ChartCommentConsent",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentRevision_CommentId",
                schema: "scores",
                table: "ChartCommentRevision",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentVote_CommentId_UserId",
                schema: "scores",
                table: "ChartCommentVote",
                columns: new[] { "CommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentVote_UserId",
                schema: "scores",
                table: "ChartCommentVote",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartComment",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ChartCommentConsent",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ChartCommentRevision",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ChartCommentVote",
                schema: "scores");
        }
    }
}
