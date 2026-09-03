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
            migrationBuilder.Sql(@"
UPDATE scores.[User] SET ImportedProfileImage = ProfileImage WHERE ImportedProfileImage IS NULL;
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
