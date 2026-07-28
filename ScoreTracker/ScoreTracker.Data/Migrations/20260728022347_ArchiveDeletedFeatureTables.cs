using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Archives the tables behind the deleted bracket subsystem and the pre-snapshot
    ///     leaderboard path. Tables are never dropped (CLAUDE.md, DATABASE-SCHEMA.md): they
    ///     leave the EF model but their rows stay queryable in the <c>archive</c> schema, so a
    ///     revived feature starts from real data rather than nothing.
    ///     <para>
    ///         EF scaffolded this as nine DropTable calls because the entities left the model.
    ///         Those were replaced by hand with schema transfers — which is also why Down moves
    ///         the tables back instead of recreating them empty, so a rollback keeps the rows.
    ///     </para>
    /// </summary>
    public partial class ArchiveDeletedFeatureTables : Migration
    {
        /// <summary>Bracket subsystem first, then the pre-snapshot leaderboard path.</summary>
        private static readonly string[] ArchivedTables =
        {
            "Match",
            "MatchLink",
            "RandomSettings",
            "TournamentPlayer",
            "TournamentMachine",
            "UserOfficialLeaderboard",
            "UserWorldRanking",
            "OfficialUserAvatar",
            "OfficialLeaderboardImportState"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF SCHEMA_ID('archive') IS NULL EXEC('CREATE SCHEMA archive');");
            foreach (var table in ArchivedTables)
                Transfer(migrationBuilder, "scores", "archive", table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in ArchivedTables)
                Transfer(migrationBuilder, "archive", "scores", table);
        }

        /// <summary>
        ///     Idempotent by object lookup: a table already sitting in the destination schema is
        ///     skipped rather than failing the run, so a partially applied migration replays.
        /// </summary>
        private static void Transfer(MigrationBuilder migrationBuilder, string from, string to, string table)
        {
            migrationBuilder.Sql(
                $"IF OBJECT_ID('{from}.{table}', 'U') IS NOT NULL " +
                $"ALTER SCHEMA {to} TRANSFER {from}.{table};");
        }
    }
}
