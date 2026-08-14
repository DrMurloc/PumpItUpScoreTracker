using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Finishes what 20260813175603_ArchiveUserTierListEntry intended: dropping the two
    ///     ON DELETE CASCADE foreign keys before the table entered the archive schema. That
    ///     migration guarded its drops with one-part names — <c>OBJECT_ID('FK_...', 'F')</c> —
    ///     which resolve against the caller's default schema (dbo for sa and the deploy login),
    ///     so the guards read NULL, the drops silently never ran, and the constraints rode the
    ///     schema transfer into <c>archive.UserTierListEntry</c> in every environment.
    ///     <para>
    ///         The applied migration stays untouched; this one drops the constraints where they
    ///         actually live now, with schema-qualified guards. An archived table is a morgue —
    ///         a live cascade FK from one would make it a participant in deletes it can no
    ///         longer be reasoned about from the model.
    ///     </para>
    /// </summary>
    public partial class DropArchivedUserTierListEntryForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF OBJECT_ID('archive.FK_UserTierListEntry_User_UserId', 'F') IS NOT NULL " +
                "ALTER TABLE archive.UserTierListEntry DROP CONSTRAINT FK_UserTierListEntry_User_UserId;");
            migrationBuilder.Sql(
                "IF OBJECT_ID('archive.FK_UserTierListEntry_Chart_ChartId', 'F') IS NOT NULL " +
                "ALTER TABLE archive.UserTierListEntry DROP CONSTRAINT FK_UserTierListEntry_Chart_ChartId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF OBJECT_ID('archive.UserTierListEntry', 'U') IS NOT NULL " +
                "AND OBJECT_ID('archive.FK_UserTierListEntry_User_UserId', 'F') IS NULL " +
                "ALTER TABLE archive.UserTierListEntry ADD CONSTRAINT FK_UserTierListEntry_User_UserId " +
                "FOREIGN KEY (UserId) REFERENCES scores.[User](Id) ON DELETE CASCADE;");
            migrationBuilder.Sql(
                "IF OBJECT_ID('archive.UserTierListEntry', 'U') IS NOT NULL " +
                "AND OBJECT_ID('archive.FK_UserTierListEntry_Chart_ChartId', 'F') IS NULL " +
                "ALTER TABLE archive.UserTierListEntry ADD CONSTRAINT FK_UserTierListEntry_Chart_ChartId " +
                "FOREIGN KEY (ChartId) REFERENCES scores.Chart(Id) ON DELETE CASCADE;");
        }
    }
}
