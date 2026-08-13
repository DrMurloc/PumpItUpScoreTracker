using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Archives UserTierListEntry, the materialized per-player relative tier list that
    ///     existed for the similar-players source of personalized Pass
    ///     (docs/design/pumbility-tier-list.md). Tables are never dropped (CLAUDE.md,
    ///     DATABASE-SCHEMA.md): the entity leaves the EF model, the table keeps its rows in
    ///     the <c>archive</c> schema.
    ///     <para>
    ///         EF scaffolded a DropTable because the entity left the model; that was replaced
    ///         by hand, which is also why Down transfers the table back rather than recreating
    ///         it empty.
    ///     </para>
    ///     <para>
    ///         Two departures from the plain transfer. The rows are DELETED first: they are
    ///         derived from scores via GetMyRelativeTierListQuery, so nothing unrecoverable is
    ///         lost, and leaving roughly a million user-keyed rows outside the account-purge
    ///         path would strand personal data behind a future deletion request — the entity
    ///         is gone from the purge manifest as of the previous commit, so nothing would ever
    ///         clear them again. The foreign keys to User and Chart are dropped too: an
    ///         archived table is a morgue, and a live FK from one would make it a participant
    ///         in cascade deletes it can no longer be reasoned about from the model.
    ///     </para>
    /// </summary>
    public partial class ArchiveUserTierListEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF SCHEMA_ID('archive') IS NULL EXEC('CREATE SCHEMA archive');");
            migrationBuilder.Sql(
                "IF OBJECT_ID('scores.UserTierListEntry', 'U') IS NOT NULL " +
                "DELETE FROM scores.UserTierListEntry;");
            migrationBuilder.Sql(
                "IF OBJECT_ID('FK_UserTierListEntry_User_UserId', 'F') IS NOT NULL " +
                "ALTER TABLE scores.UserTierListEntry DROP CONSTRAINT FK_UserTierListEntry_User_UserId;");
            migrationBuilder.Sql(
                "IF OBJECT_ID('FK_UserTierListEntry_Chart_ChartId', 'F') IS NOT NULL " +
                "ALTER TABLE scores.UserTierListEntry DROP CONSTRAINT FK_UserTierListEntry_Chart_ChartId;");
            migrationBuilder.Sql(
                "IF OBJECT_ID('scores.UserTierListEntry', 'U') IS NOT NULL " +
                "ALTER SCHEMA archive TRANSFER scores.UserTierListEntry;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF OBJECT_ID('archive.UserTierListEntry', 'U') IS NOT NULL " +
                "ALTER SCHEMA scores TRANSFER archive.UserTierListEntry;");
            migrationBuilder.Sql(
                "IF OBJECT_ID('scores.UserTierListEntry', 'U') IS NOT NULL " +
                "AND OBJECT_ID('FK_UserTierListEntry_User_UserId', 'F') IS NULL " +
                "ALTER TABLE scores.UserTierListEntry ADD CONSTRAINT FK_UserTierListEntry_User_UserId " +
                "FOREIGN KEY (UserId) REFERENCES scores.[User](Id) ON DELETE CASCADE;");
            migrationBuilder.Sql(
                "IF OBJECT_ID('scores.UserTierListEntry', 'U') IS NOT NULL " +
                "AND OBJECT_ID('FK_UserTierListEntry_Chart_ChartId', 'F') IS NULL " +
                "ALTER TABLE scores.UserTierListEntry ADD CONSTRAINT FK_UserTierListEntry_Chart_ChartId " +
                "FOREIGN KEY (ChartId) REFERENCES scores.Chart(Id) ON DELETE CASCADE;");
        }
    }
}
