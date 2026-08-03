using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     A maker's private note on one invite link.
    ///     <para>
    ///         Nullable and unindexed on purpose: it is a reminder, not data anything queries. A
    ///         maker with four links has no way to tell which one they posted where, and revoking
    ///         the wrong one silently cuts off whoever was using it.
    ///     </para>
    /// </summary>
    public partial class ToolInviteNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "scores",
                table: "ToolInviteCode",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                schema: "scores",
                table: "ToolInviteCode");
        }
    }
}
