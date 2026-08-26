using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Archives ChartSkill, the per-chart rows of the retired eleven-skill rollup
    ///     (docs/design/nuke-old-skill-categories.md). Tables are never dropped (CLAUDE.md,
    ///     DATABASE-SCHEMA.md): the entity leaves the EF model, the table keeps its rows in the
    ///     <c>archive</c> schema.
    ///     <para>
    ///         EF scaffolded a DropTable because the entity left the model; that was replaced by
    ///         hand, which is also why Down transfers the table back rather than recreating it
    ///         empty.
    ///     </para>
    ///     <para>
    ///         The rows are kept rather than deleted. They are chart-keyed, not user-keyed, so
    ///         nothing personal is stranded — and they were the live tags at the moment the
    ///         rollup died, which is the one snapshot nothing else holds. Its sibling
    ///         <c>ChartSkillArchive</c> stays in <c>scores</c> and stays mapped: the Chabala lens
    ///         reads it, so it is a live table with one reader rather than a morgue.
    ///     </para>
    /// </summary>
    public partial class ArchiveChartSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF SCHEMA_ID('archive') IS NULL EXEC('CREATE SCHEMA archive');");
            migrationBuilder.Sql(
                "IF OBJECT_ID('scores.ChartSkill', 'U') IS NOT NULL " +
                "ALTER SCHEMA archive TRANSFER scores.ChartSkill;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF OBJECT_ID('archive.ChartSkill', 'U') IS NOT NULL " +
                "ALTER SCHEMA scores TRANSFER archive.ChartSkill;");
        }
    }
}
