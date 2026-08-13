using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     The moderation tables, plus the permission backfill: ModerateComments (1 &lt;&lt; 4)
    ///     joins the default admin kit, so the two stored populations tracking the old composed
    ///     values move with it — 13 (the seed, deliberately without PromoteAdmins) becomes 29 and
    ///     15 (explicit All) becomes 31, across BOTH CommunityMembership.Permissions and
    ///     Community.DefaultAdminPermissions. A hand-picked subset is left alone. Missing the
    ///     second table means every FUTURE admin in an existing club silently lacks the power.
    /// </summary>
    public partial class ChartCommentModeration : Migration
    {
        // Public so the integration test can execute the exact production SQL against seeded
        // rows — the migration itself always runs against empty tables in test fixtures.
        // Forward-only: once run, a backfilled 29 is indistinguishable from a hand-picked one,
        // so Down() deliberately does not attempt to reverse it.
        // CommunityTests.PermissionValuesMatchTheModerationBackfill pins these literals.
        public const string BackfillSql = """
            UPDATE [scores].[CommunityMembership] SET [Permissions] = 29 WHERE [Permissions] = 13;
            UPDATE [scores].[CommunityMembership] SET [Permissions] = 31 WHERE [Permissions] = 15;
            UPDATE [scores].[Community] SET [DefaultAdminPermissions] = 29 WHERE [DefaultAdminPermissions] = 13;
            UPDATE [scores].[Community] SET [DefaultAdminPermissions] = 31 WHERE [DefaultAdminPermissions] = 15;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartCommentReport",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RenderingLocale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CommunityResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CommunityResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SiteResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SiteResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartCommentReport", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartCommentRestriction",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestrictedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LiftedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartCommentRestriction", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentReport_CommentId",
                schema: "scores",
                table: "ChartCommentReport",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentReport_ReporterUserId",
                schema: "scores",
                table: "ChartCommentReport",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentRestriction_CommunityId",
                schema: "scores",
                table: "ChartCommentRestriction",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentRestriction_UserId_CommunityId",
                schema: "scores",
                table: "ChartCommentRestriction",
                columns: new[] { "UserId", "CommunityId" });

            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartCommentReport",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ChartCommentRestriction",
                schema: "scores");
        }
    }
}
