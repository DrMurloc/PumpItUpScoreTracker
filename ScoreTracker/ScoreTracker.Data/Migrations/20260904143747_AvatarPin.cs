using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvatarPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvatarIsPinned",
                schema: "scores",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ImportedProfileImage",
                schema: "scores",
                table: "User",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            // Backfill: every existing row's ProfileImage IS what the last import gave it, because
            // until now nothing else could write it. Seeding the new column with it means "Back to
            // Auto" restores a real picture for everyone on day one, instead of only for players
            // who happen to import again after this deploys.
            //
            // The length guard is structural, not decorative: ProfileImage is nvarchar(max) and
            // this column is nvarchar(400). Nothing in the data comes close today (the longest is
            // 79 characters), but a silent truncation would write a url resolving to nothing and
            // only surface the first time someone pressed "Back to Auto".
            //
            // Wrapped in EXEC because the column it writes is added in this same migration. An
            // idempotent script emits a migration as ONE batch, and SQL Server compiles a batch
            // whole before running any of it — so a direct UPDATE fails with "Invalid column
            // name 'ImportedProfileImage'" and takes the entire migration with it. Deferring the
            // compile to execution time is the fix, and it makes this correct under every
            // application path rather than only the bundle's one-command-per-operation one.
            migrationBuilder.Sql(@"
EXEC(N'
UPDATE scores.[User]
SET ImportedProfileImage = ProfileImage
WHERE ImportedProfileImage IS NULL AND LEN(ProfileImage) <= 400;
');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarIsPinned",
                schema: "scores",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ImportedProfileImage",
                schema: "scores",
                table: "User");
        }
    }
}
