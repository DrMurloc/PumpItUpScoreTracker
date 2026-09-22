using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Moves the two RISE mixes under Phoenix 2 and above Phoenix in the picker's order (owner,
    ///     2026-09-22): RISE launched in 2025, between the two Phoenix generations, and the picker reads the
    ///     order descending, so Rise outranks its Arcade Station. Mirrors the enum's DisplayOrder; data only.
    /// </summary>
    public partial class RiseMixOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [scores].[Mix] SET [SortOrder] = 276 WHERE [Id] = '8FF3F8AA-3870-4FFB-85F6-97340E091506'; -- Rise
UPDATE [scores].[Mix] SET [SortOrder] = 274 WHERE [Id] = '71B55C75-38BE-492C-97D3-29BE828C83B6'; -- RiseArcade
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [scores].[Mix] SET [SortOrder] = 290 WHERE [Id] = '8FF3F8AA-3870-4FFB-85F6-97340E091506'; -- Rise
UPDATE [scores].[Mix] SET [SortOrder] = 300 WHERE [Id] = '71B55C75-38BE-492C-97D3-29BE828C83B6'; -- RiseArcade
");
        }
    }
}
