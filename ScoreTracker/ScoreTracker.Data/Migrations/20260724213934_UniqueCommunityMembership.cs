using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCommunityMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommunityMembership_CommunityId_UserId",
                schema: "scores",
                table: "CommunityMembership");

            // The index cannot be created while a duplicate seat exists, and a migration that
            // throws blocks the whole deploy. Collapse any duplicate to the row carrying the most
            // standing: a ban has to survive because it is what blocks rejoin, then the creator
            // seat, then an admin's permissions, then a plain member.
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT Id, ROW_NUMBER() OVER (
                        PARTITION BY CommunityId, UserId
                        ORDER BY CASE Role WHEN 'Banned'  THEN 0
                                           WHEN 'Creator' THEN 1
                                           WHEN 'Admin'   THEN 2
                                           ELSE 3 END, JoinedAt, Id) AS Seat
                    FROM scores.CommunityMembership)
                DELETE FROM scores.CommunityMembership
                WHERE Id IN (SELECT Id FROM ranked WHERE Seat > 1);");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMembership_CommunityId_UserId",
                schema: "scores",
                table: "CommunityMembership",
                columns: new[] { "CommunityId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommunityMembership_CommunityId_UserId",
                schema: "scores",
                table: "CommunityMembership");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMembership_CommunityId_UserId",
                schema: "scores",
                table: "CommunityMembership",
                columns: new[] { "CommunityId", "UserId" });
        }
    }
}
