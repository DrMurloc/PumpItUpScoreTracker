using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Seeds the all-tools sharing preference from IsPublic, once, at rollout.
    ///     <para>
    ///         This is a one-time migration, not a rule. Public and all-tools stay separate concepts:
    ///         a player who goes public later has the switch turned on by the Account page's own
    ///         logic, and the two are never joined in a query. Seeding here is what lets the launch
    ///         announcement say "sharing is on" truthfully to the players it is on for.
    ///     </para>
    ///     <para>
    ///         Idempotent — it only inserts where no preference row exists — because a migration that
    ///         is replayed against a partially-migrated database must not clobber a choice a player
    ///         has already made.
    ///     </para>
    /// </summary>
    public partial class SeedToolSharePreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO scores.ToolSharePreference (UserId, ShareWithAllTools, SetAt)
SELECT u.Id, 1, SYSDATETIMEOFFSET()
FROM scores.[User] u
WHERE u.IsPublic = 1
  AND NOT EXISTS (SELECT 1 FROM scores.ToolSharePreference p WHERE p.UserId = u.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the rows this seed could have created. A player who has since set the preference
            // by hand carries a later SetAt and is left alone — an undo must not silently reverse a
            // deliberate choice.
            migrationBuilder.Sql(@"
DELETE p FROM scores.ToolSharePreference p
INNER JOIN scores.[User] u ON u.Id = p.UserId
WHERE u.IsPublic = 1 AND p.ShareWithAllTools = 1;");
        }
    }
}
