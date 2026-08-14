using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     scores.PumbilityCensusEntry → scores.PumbilityTierListEntry: the PUMBILITY pool
    ///     counts are tier lists and carry tier-list-family names (owner, 2026-08-13;
    ///     docs/design/pumbility-tier-list.md §8).
    ///     <para>
    ///         EF scaffolded a DropTable + CreateTable because the entity's CLR type changed and
    ///         a rename cannot be inferred; that was replaced by hand with a true rename so any
    ///         rows already built survive — tables are never dropped. The primary key is renamed
    ///         with a schema-qualified sp_rename, because an unqualified OBJECT_ID resolves
    ///         against the caller's default schema and silently misses constraints in scores.
    ///     </para>
    /// </summary>
    public partial class RenamePumbilityCensusToTierListEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PumbilityCensusEntry",
                schema: "scores",
                newName: "PumbilityTierListEntry",
                newSchema: "scores");

            migrationBuilder.Sql(
                "IF OBJECT_ID('scores.PK_PumbilityCensusEntry', 'PK') IS NOT NULL " +
                "EXEC sp_rename N'scores.PK_PumbilityCensusEntry', N'PK_PumbilityTierListEntry', N'OBJECT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF OBJECT_ID('scores.PK_PumbilityTierListEntry', 'PK') IS NOT NULL " +
                "EXEC sp_rename N'scores.PK_PumbilityTierListEntry', N'PK_PumbilityCensusEntry', N'OBJECT';");

            migrationBuilder.RenameTable(
                name: "PumbilityTierListEntry",
                schema: "scores",
                newName: "PumbilityCensusEntry",
                newSchema: "scores");
        }
    }
}
