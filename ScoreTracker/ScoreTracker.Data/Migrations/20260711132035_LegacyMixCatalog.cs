using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class LegacyMixCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                schema: "scores",
                table: "Mix",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "scores",
                table: "Mix",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Legacy mix rows (docs/design/legacy-mixes.md). Ids are minted constants shared
            // with MixIds.cs and tools/PumpoutExtractor/MixMap.cs — the backfill scripts the
            // tool generates reference exactly these ids. IF NOT EXISTS keeps the seed safe
            // if a row was ever hand-inserted ahead of deploy (the Phoenix 2 precedent).
            migrationBuilder.Sql(@"
-- The primary trio predates migration-managed seeding (prod rows were hand-run
-- scripts), so INSERT-if-missing makes fresh local databases self-consistent,
-- then the UPDATEs set the picker columns everywhere including prod.
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '20F8CCF8-94B1-418D-B923-C375B042BDA8') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('20F8CCF8-94B1-418D-B923-C375B042BDA8', N'XX', 260, 1);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B', N'Phoenix', 270, 1);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948', N'Phoenix2', 280, 1);
UPDATE [scores].[Mix] SET [SortOrder] = 260, [IsPrimary] = 1 WHERE [Id] = '20F8CCF8-94B1-418D-B923-C375B042BDA8'; -- XX
UPDATE [scores].[Mix] SET [SortOrder] = 270, [IsPrimary] = 1 WHERE [Id] = '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B'; -- Phoenix
UPDATE [scores].[Mix] SET [SortOrder] = 280, [IsPrimary] = 1 WHERE [Id] = 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948'; -- Phoenix2

IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '4FDCE23C-904C-4538-952F-DDA636D1B154') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('4FDCE23C-904C-4538-952F-DDA636D1B154', N'1st', 10, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '6558B48D-9EF2-4A51-BC0E-8A0956469D01') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('6558B48D-9EF2-4A51-BC0E-8A0956469D01', N'2nd', 20, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '72A67D8A-DD28-470D-9857-CDE789BCAFD7') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('72A67D8A-DD28-470D-9857-CDE789BCAFD7', N'3rd', 30, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '38D59ECF-F5E0-42A3-9111-796EB398FFEB') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('38D59ECF-F5E0-42A3-9111-796EB398FFEB', N'OBG SE', 40, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '34CEB319-84FA-4F2D-A48C-98DC861DA3FB') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('34CEB319-84FA-4F2D-A48C-98DC861DA3FB', N'Collection', 50, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'F680D1E5-C4F8-4479-8423-CBF59C1512D6') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('F680D1E5-C4F8-4479-8423-CBF59C1512D6', N'Perfect', 60, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '84562821-C87E-4346-B0C1-38A7DFA5637F') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('84562821-C87E-4346-B0C1-38A7DFA5637F', N'Extra', 70, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'FD9A0B6A-F241-47A0-980A-F7CB518A8081') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('FD9A0B6A-F241-47A0-980A-F7CB518A8081', N'Premiere', 80, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '084B06F5-5E8A-47BC-8307-442DB8000C5B') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('084B06F5-5E8A-47BC-8307-442DB8000C5B', N'Prex', 90, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'CE37A838-2CAD-40F4-ACC0-A67D6FB97239') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('CE37A838-2CAD-40F4-ACC0-A67D6FB97239', N'Rebirth', 100, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'C995A044-E897-4730-B8E9-599B822BCA0D') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('C995A044-E897-4730-B8E9-599B822BCA0D', N'Premiere 2', 110, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '953CC701-4A64-4E4B-BBB3-51C7D66BDAE6') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('953CC701-4A64-4E4B-BBB3-51C7D66BDAE6', N'Prex 2', 120, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'A409D148-8167-4065-A351-5EC45A863F1A') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('A409D148-8167-4065-A351-5EC45A863F1A', N'Premiere 3', 130, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '94BD6973-8CEC-48D7-AFF2-B310B3B0B0FE') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('94BD6973-8CEC-48D7-AFF2-B310B3B0B0FE', N'Prex 3', 140, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '69D234A7-4141-4A69-AC55-114B7164198D') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('69D234A7-4141-4A69-AC55-114B7164198D', N'Exceed', 150, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '4B9842C7-EE1B-4B0E-A370-9A966994236A') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('4B9842C7-EE1B-4B0E-A370-9A966994236A', N'Exceed 2', 160, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '4A18B364-4B9D-42F3-AE79-222CF1D4ED7B') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('4A18B364-4B9D-42F3-AE79-222CF1D4ED7B', N'Zero', 170, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '07CB82DD-D577-41EA-BA9E-9746061752C1') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('07CB82DD-D577-41EA-BA9E-9746061752C1', N'NX', 180, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '00D66EAF-5408-46F1-A88E-74406891C9D6') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('00D66EAF-5408-46F1-A88E-74406891C9D6', N'Pro', 185, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'DF15FB43-5E13-4941-A7AE-D979F8FD6220') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('DF15FB43-5E13-4941-A7AE-D979F8FD6220', N'NX2', 190, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'D4C22342-F0EA-4F8F-9C5B-BE75ACC980FA') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('D4C22342-F0EA-4F8F-9C5B-BE75ACC980FA', N'NXA', 200, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '745660B3-15DB-42D1-AD0C-0EE775503F62') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('745660B3-15DB-42D1-AD0C-0EE775503F62', N'Pro 2', 205, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '178562FC-740F-46C6-B957-0A0381CCCFC4') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('178562FC-740F-46C6-B957-0A0381CCCFC4', N'Fiesta', 210, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '90C0A1E0-0DE6-4D05-A035-533669224482') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('90C0A1E0-0DE6-4D05-A035-533669224482', N'Fiesta EX', 220, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'E172B206-ACF9-4A52-A6FE-CBF56FE15167') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('E172B206-ACF9-4A52-A6FE-CBF56FE15167', N'Fiesta 2', 230, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '363B8D21-2DDE-4CE0-A54E-2AEE2B7280A2') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('363B8D21-2DDE-4CE0-A54E-2AEE2B7280A2', N'Infinity', 235, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = 'D8316882-8D08-4993-B692-D0608392FB02') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('D8316882-8D08-4993-B692-D0608392FB02', N'Prime', 240, 0);
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = '00E93A6B-9C39-452F-96B0-1DF42DBDD0AC') INSERT INTO [scores].[Mix] ([Id], [Name], [SortOrder], [IsPrimary]) VALUES ('00E93A6B-9C39-452F-96B0-1DF42DBDD0AC', N'Prime 2', 250, 0);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [scores].[Mix] WHERE [Id] IN (
'4FDCE23C-904C-4538-952F-DDA636D1B154','6558B48D-9EF2-4A51-BC0E-8A0956469D01','72A67D8A-DD28-470D-9857-CDE789BCAFD7',
'38D59ECF-F5E0-42A3-9111-796EB398FFEB','34CEB319-84FA-4F2D-A48C-98DC861DA3FB','F680D1E5-C4F8-4479-8423-CBF59C1512D6',
'84562821-C87E-4346-B0C1-38A7DFA5637F','FD9A0B6A-F241-47A0-980A-F7CB518A8081','084B06F5-5E8A-47BC-8307-442DB8000C5B',
'CE37A838-2CAD-40F4-ACC0-A67D6FB97239','C995A044-E897-4730-B8E9-599B822BCA0D','953CC701-4A64-4E4B-BBB3-51C7D66BDAE6',
'A409D148-8167-4065-A351-5EC45A863F1A','94BD6973-8CEC-48D7-AFF2-B310B3B0B0FE','69D234A7-4141-4A69-AC55-114B7164198D',
'4B9842C7-EE1B-4B0E-A370-9A966994236A','4A18B364-4B9D-42F3-AE79-222CF1D4ED7B','07CB82DD-D577-41EA-BA9E-9746061752C1',
'00D66EAF-5408-46F1-A88E-74406891C9D6','DF15FB43-5E13-4941-A7AE-D979F8FD6220','D4C22342-F0EA-4F8F-9C5B-BE75ACC980FA',
'745660B3-15DB-42D1-AD0C-0EE775503F62','178562FC-740F-46C6-B957-0A0381CCCFC4','90C0A1E0-0DE6-4D05-A035-533669224482',
'E172B206-ACF9-4A52-A6FE-CBF56FE15167','363B8D21-2DDE-4CE0-A54E-2AEE2B7280A2','D8316882-8D08-4993-B692-D0608392FB02',
'00E93A6B-9C39-452F-96B0-1DF42DBDD0AC');
");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                schema: "scores",
                table: "Mix");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "scores",
                table: "Mix");
        }
    }
}
