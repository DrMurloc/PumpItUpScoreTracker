using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Repairs avatars clobbered by the broken-avatar bug: when the official-site
    ///     scrape yielded no recognizable avatar file, the import persisted the bare
    ///     /avatars/ directory URL over the player's real avatar (a broken image on
    ///     every page). Those rows reset to the default avatar; the next import — with
    ///     the scrape fix in place — restores each player's real one.
    /// </summary>
    public partial class RepairBareAvatarUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [scores].[User]
                SET ProfileImage = 'https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png'
                WHERE ProfileImage IS NULL OR ProfileImage = '' OR ProfileImage LIKE '%/avatars/';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only repair: the clobbered values were garbage; nothing to restore.
        }
    }
}
