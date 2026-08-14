using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoMTables : Migration
    {
        /// <summary>
        ///     Copies every legacy MoM tournament (scores.Tournament, IsMoM = 1) onto the MoM*
        ///     tables — docs/design/march-of-murlocs.md §7. Copy, never move (D7): the legacy rows
        ///     stay untouched. Predicate-driven (the junk signature EndDate &lt; StartDate is
        ///     excluded, §9.1) and idempotent (every INSERT is guarded), so the integration suite
        ///     can seed legacy shapes and run this same script. Public const so the test executes
        ///     the exact bytes production ran. Frozen with this migration once applied — a fix is
        ///     a NEW migration, never an edit here.
        /// </summary>
        public const string LegacyCopySql = @"
DECLARE @PhoenixMixId uniqueidentifier = '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B'; -- MixIds.Phoenix; all copied sessions are Phoenix (verified 62/62 in prod)

SELECT t.Id,
       t.Name,
       t.StartDate,
       t.EndDate,
       t.Configuration,
       CASE WHEN t.Name LIKE '% - Singles' THEN CAST(0 AS tinyint) ELSE CAST(1 AS tinyint) END AS ChartType, -- ChartType.Single / ChartType.Double
       CASE WHEN t.Name LIKE '% - Singles' OR t.Name LIKE '% - Doubles'
            THEN LEFT(t.Name, LEN(t.Name) - 10) ELSE t.Name END AS SeasonName
INTO #legacy
FROM scores.Tournament t
WHERE t.IsMoM = 1 AND t.EndDate >= t.StartDate;

-- Seasons: Singles/Doubles pairs sharing a stripped name collapse into one season. A season
-- is quarterly (Year/Quarter set) when it ends on the last day of March, June, September or
-- December — the cadence Winter 2025 started; everything earlier is off-grid (NULL).
INSERT INTO scores.MoMSeason (Id, [Year], Quarter, Name, StartsAt, EndsAt, CreatedAt)
SELECT NEWID(),
       CASE WHEN MONTH(g.EndsAt) IN (3, 6, 9, 12) AND DAY(g.EndsAt) = DAY(EOMONTH(CAST(g.EndsAt AS date)))
            THEN YEAR(g.EndsAt) END,
       CASE WHEN MONTH(g.EndsAt) IN (3, 6, 9, 12) AND DAY(g.EndsAt) = DAY(EOMONTH(CAST(g.EndsAt AS date)))
            THEN MONTH(g.EndsAt) / 3 END,
       g.SeasonName, g.StartsAt, g.EndsAt, SYSDATETIMEOFFSET()
FROM (
    SELECT SeasonName, MIN(StartDate) AS StartsAt, MAX(EndDate) AS EndsAt
    FROM #legacy
    GROUP BY SeasonName
) g
WHERE NOT EXISTS (SELECT 1 FROM scores.MoMSeason s WHERE s.Name = g.SeasonName);

-- Boards: one per legacy tournament, keeping the legacy tournament's Guid as the board's —
-- which is what keeps every old /Tournament/Stamina URL resolving. The frozen config is the
-- legacy Configuration JSON verbatim, so historical sessions re-price byte-identically.
-- (No literal braces anywhere in this script: ExecuteSqlRawAsync composite-formats its SQL,
-- and the integration suite runs these exact bytes through it.)
INSERT INTO scores.MoMBoard (Id, SeasonId, MixId, ChartType, ScoringConfig)
SELECT l.Id, s.Id, @PhoenixMixId, l.ChartType, l.Configuration
FROM #legacy l
JOIN scores.MoMSeason s ON s.Name = l.SeasonName
WHERE NOT EXISTS (SELECT 1 FROM scores.MoMBoard b WHERE b.Id = l.Id);

-- Balance snapshot, DELTA ROWS ONLY (§9.3): a row equal to the chart's folder level + 0.5 is
-- byte-identical to no row at all, so those are never copied. Measured -60% on real seasons.
INSERT INTO scores.MoMChartLevel (SeasonId, MixId, ChartId, Level)
SELECT b.SeasonId, b.MixId, tcl.ChartId, MAX(tcl.Level)
FROM scores.TournamentChartLevel tcl
JOIN scores.MoMBoard b ON b.Id = tcl.TournamentId
JOIN scores.ChartMix cm ON cm.ChartId = tcl.ChartId AND cm.MixId = b.MixId
WHERE ABS(tcl.Level - (cm.Level + 0.5)) > 0.0001
  AND NOT EXISTS (SELECT 1 FROM scores.MoMChartLevel e
                  WHERE e.SeasonId = b.SeasonId AND e.MixId = b.MixId AND e.ChartId = tcl.ChartId)
GROUP BY b.SeasonId, b.MixId, tcl.ChartId;

-- Sessions. PublishedAt backfills to the season's EndsAt (owner, 2026-08-14): NULL means
-- draft, the legacy rows carry no timestamp of any kind, and the value feeds only
-- tie-breaks, which never happen. RestTime converts to ticks. The derived cache columns
-- aggregate the entry JSON joined to the season snapshot: balanced level is the snapshot
-- override where one exists, folder level + 0.5 where none does (§9.3); the grade ladder is
-- Phoenix 1's ScoreRange floors (PhoenixLetterGrade.cs) with the enum ordinal as the value.
INSERT INTO scores.MoMSession (Id, BoardId, UserId, PublishedAt, TotalScore, ChartsPlayed, RestTime,
                               AverageDifficulty, AverageGrade, LowestLevel, HighestLevel, VideoUrl,
                               CreatedAt, UpdatedAt)
SELECT uts.Id, b.Id, uts.UserId, s.EndsAt, uts.SessionScore, uts.ChartsPlayed,
       DATEDIFF_BIG(MICROSECOND, CAST('00:00:00' AS time), uts.RestTime) * 10,
       agg.AvgBalanced, agg.AvgGrade, agg.MinLevel, agg.MaxLevel,
       LEFT(uts.VideoUrl, 500), s.EndsAt, s.EndsAt
FROM scores.UserTournamentSession uts
JOIN scores.MoMBoard b ON b.Id = uts.TournamentId
JOIN scores.MoMSeason s ON s.Id = b.SeasonId
CROSS APPLY (
    SELECT AVG(COALESCE(tcl.Level, cm.Level + 0.5)) AS AvgBalanced,
           AVG(CAST(CASE
               WHEN e.Score >= 995000 THEN 15 WHEN e.Score >= 990000 THEN 14
               WHEN e.Score >= 985000 THEN 13 WHEN e.Score >= 980000 THEN 12
               WHEN e.Score >= 975000 THEN 11 WHEN e.Score >= 970000 THEN 10
               WHEN e.Score >= 960000 THEN 9  WHEN e.Score >= 950000 THEN 8
               WHEN e.Score >= 925000 THEN 7  WHEN e.Score >= 900000 THEN 6
               WHEN e.Score >= 825000 THEN 5  WHEN e.Score >= 750000 THEN 4
               WHEN e.Score >= 650000 THEN 3  WHEN e.Score >= 550000 THEN 2
               WHEN e.Score >= 450000 THEN 1  ELSE 0 END AS float)) AS AvgGrade,
           MIN(cm.Level) AS MinLevel,
           MAX(cm.Level) AS MaxLevel
    FROM OPENJSON(uts.ChartEntries) j
    CROSS APPLY (SELECT TRY_CAST(JSON_VALUE(j.value, '$.ChartId') AS uniqueidentifier) AS ChartId,
                        TRY_CAST(JSON_VALUE(j.value, '$.Score') AS int) AS Score) e
    JOIN scores.ChartMix cm ON cm.ChartId = e.ChartId AND cm.MixId = b.MixId
    LEFT JOIN scores.TournamentChartLevel tcl
        ON tcl.TournamentId = uts.TournamentId AND tcl.ChartId = e.ChartId
) agg
WHERE NOT EXISTS (SELECT 1 FROM scores.MoMSession m WHERE m.Id = uts.Id);

-- Session charts, exploded from the legacy JSON blob. Ordinal is the array index.
-- BonusPoints exists only in newer blobs (14 of 62 sessions) and coalesces to 0 — the
-- base/bonus split is unrecoverable for the rest and SessionScore stays the stored truth.
-- PlayedAt stays NULL until timestamps land (Slice 3).
INSERT INTO scores.MoMSessionChart (SessionId, Ordinal, ChartId, Score, Plate, IsBroken,
                                    SessionScore, BonusPoints, PlayedAt)
SELECT m.Id, CAST(j.[key] AS int),
       TRY_CAST(JSON_VALUE(j.value, '$.ChartId') AS uniqueidentifier),
       TRY_CAST(JSON_VALUE(j.value, '$.Score') AS int),
       JSON_VALUE(j.value, '$.Plate'),
       CASE WHEN JSON_VALUE(j.value, '$.IsBroken') = 'true' THEN 1 ELSE 0 END,
       TRY_CAST(JSON_VALUE(j.value, '$.SessionScore') AS int),
       COALESCE(TRY_CAST(JSON_VALUE(j.value, '$.BonusPoints') AS int), 0),
       NULL
FROM scores.MoMSession m
JOIN scores.UserTournamentSession uts ON uts.Id = m.Id
CROSS APPLY OPENJSON(uts.ChartEntries) j
WHERE NOT EXISTS (SELECT 1 FROM scores.MoMSessionChart e
                  WHERE e.SessionId = m.Id AND e.Ordinal = CAST(j.[key] AS int));

DROP TABLE #legacy;
";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoMSeason",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: true),
                    Quarter = table.Column<byte>(type: "tinyint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoMSeason", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MoMBoard",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartType = table.Column<byte>(type: "tinyint", nullable: false),
                    ScoringConfig = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoMBoard", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoMBoard_MoMSeason_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "scores",
                        principalTable: "MoMSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MoMChartLevel",
                schema: "scores",
                columns: table => new
                {
                    SeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoMChartLevel", x => new { x.SeasonId, x.MixId, x.ChartId });
                    table.ForeignKey(
                        name: "FK_MoMChartLevel_MoMSeason_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "scores",
                        principalTable: "MoMSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MoMSession",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TotalScore = table.Column<int>(type: "int", nullable: false),
                    ChartsPlayed = table.Column<int>(type: "int", nullable: false),
                    RestTime = table.Column<long>(type: "bigint", nullable: false),
                    AverageDifficulty = table.Column<double>(type: "float", nullable: false),
                    AverageGrade = table.Column<double>(type: "float", nullable: false),
                    LowestLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    HighestLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoMSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoMSession_MoMBoard_BoardId",
                        column: x => x.BoardId,
                        principalSchema: "scores",
                        principalTable: "MoMBoard",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MoMSessionChart",
                schema: "scores",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsBroken = table.Column<bool>(type: "bit", nullable: false),
                    SessionScore = table.Column<int>(type: "int", nullable: false),
                    BonusPoints = table.Column<int>(type: "int", nullable: false),
                    PlayedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoMSessionChart", x => new { x.SessionId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_MoMSessionChart_MoMSession_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "scores",
                        principalTable: "MoMSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoMBoard_SeasonId_MixId_ChartType",
                schema: "scores",
                table: "MoMBoard",
                columns: new[] { "SeasonId", "MixId", "ChartType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MoMSeason_Quarter",
                schema: "scores",
                table: "MoMSeason",
                columns: new[] { "Year", "Quarter" },
                unique: true,
                filter: "[Quarter] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MoMSession_Board",
                schema: "scores",
                table: "MoMSession",
                columns: new[] { "BoardId", "TotalScore" },
                descending: new[] { false, true },
                filter: "[PublishedAt] IS NOT NULL");

            migrationBuilder.Sql(LegacyCopySql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoMChartLevel",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "MoMSessionChart",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "MoMSession",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "MoMBoard",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "MoMSeason",
                schema: "scores");
        }
    }
}
