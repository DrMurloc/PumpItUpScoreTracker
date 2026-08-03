using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Fills in PIU Tracker's source repository, which arrived after the requirement shipped.
    ///     <para>
    ///         A new migration rather than an edit to <c>SeedPiuTrackerTool</c>: that one has already
    ///         run against local and test databases, so editing it would change nothing where it ran
    ///         and quietly alter every build-from-scratch environment that replays the chain.
    ///     </para>
    ///     <para>
    ///         This does <b>not</b> lift the grandfather in <c>GrandfatheredTools</c>. The gate wants
    ///         a repository that has been <i>checked</i> and a handle to reach the maker on, and
    ///         neither is something a migration can honestly assert — stamping a check date here
    ///         would record a fetch that never happened, which is the exact failure the check exists
    ///         to catch. An admin presses Check the link and adds TUSA's handle; the constant goes in
    ///         the commit after that.
    ///     </para>
    ///     <para>Idempotent, and it will not overwrite a URL an admin has already set by hand.</para>
    /// </summary>
    public partial class SeedPiuTrackerRepository : Migration
    {
        /// <summary>Matches <c>GrandfatheredTools.PiuTracker</c> and the seed that created the row.</summary>
        private const string ToolId = "7B1B7F8E-6F1E-4C4B-9F3E-2C0D5A9E4B10";

        private const string RepositoryUrl = "https://github.com/AlanCooper509/phoenix-parser";

        /// <summary>
        ///     The first path segment, which is what <c>Tool</c> parses and what the admin list prints
        ///     beside the link. It is a GitHub account rather than the name TUSA goes by here, so the
        ///     two will not match at a glance — that is expected, and it is why the parsed owner is
        ///     shown to a human rather than decided on.
        /// </summary>
        private const string RepositoryOwner = "AlanCooper509";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
UPDATE scores.Tool
SET RepositoryUrl = '{RepositoryUrl}',
    RepositoryOwner = '{RepositoryOwner}'
WHERE Id = '{ToolId}' AND RepositoryUrl IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
UPDATE scores.Tool
SET RepositoryUrl = NULL,
    RepositoryOwner = NULL,
    RepositoryCheckedAt = NULL
WHERE Id = '{ToolId}' AND RepositoryUrl = '{RepositoryUrl}';");
        }
    }
}
