using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Seeds the two Pump It Up RISE mixes — the keyboard game and its Arcade Station — as
    ///     primary picker entries after Phoenix 2 (docs/design/rise.md D1, D13). Data only, no
    ///     model change; the ids are minted once and mirrored in MixIds and tools/RiseCatalog.
    ///     Their songs, charts, membership rows and patches arrive by that tool's scripts, never
    ///     by migration, like every other catalog batch.
    /// </summary>
    public partial class RiseMixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '8FF3F8AA-3870-4FFB-85F6-97340E091506') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('8FF3F8AA-3870-4FFB-85F6-97340E091506', N'Rise', 290, 1);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '71B55C75-38BE-492C-97D3-29BE828C83B6') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('71B55C75-38BE-492C-97D3-29BE828C83B6', N'RiseArcade', 300, 1);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [scores].[Mix] WHERE [Id] IN ('8FF3F8AA-3870-4FFB-85F6-97340E091506', '71B55C75-38BE-492C-97D3-29BE828C83B6');
");
        }
    }
}
