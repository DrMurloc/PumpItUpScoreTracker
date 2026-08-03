using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Whether a tool reads scores at all, or is only a directory entry pointing at a site.
    ///     <para>
    ///         Stated by the maker at registration rather than derived from whether keys exist: a
    ///         brand-new integrated tool has none either, for the thirty seconds before its maker
    ///         mints one, and the directory would offer players a Visit button for a tool that reads
    ///         scores.
    ///     </para>
    ///     <para>
    ///         Backfilled <c>Integrated</c> — every tool that exists today reads scores, PIU Tracker
    ///         included. Not nullable: unlike the source and handle columns, this one has a correct
    ///         answer for every existing row.
    ///     </para>
    /// </summary>
    public partial class ToolKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "scores",
                table: "Tool",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Integrated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "scores",
                table: "Tool");
        }
    }
}
