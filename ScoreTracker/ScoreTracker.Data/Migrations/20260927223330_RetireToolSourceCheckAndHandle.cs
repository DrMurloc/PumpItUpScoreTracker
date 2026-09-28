using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Takes <c>scores.Tool.RepositoryCheckedAt</c> and <c>scores.Tool.DiscordHandle</c> out of the
    ///     model and leaves both columns, and what is in them, where they are. Nothing reads or writes
    ///     them any more — a tool reaches other players through its maker's linked Discord account —
    ///     but the handles makers typed are the one way to reach anyone who has not linked yet.
    ///     <para>
    ///         Scaffolded as two column drops and emptied by hand. Both columns are nullable, so rows
    ///         the model inserts without them are unaffected.
    ///     </para>
    /// </summary>
    public partial class RetireToolSourceCheckAndHandle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
