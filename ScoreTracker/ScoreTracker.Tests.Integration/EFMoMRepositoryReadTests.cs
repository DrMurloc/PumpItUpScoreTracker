using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The MoM read surface against a real migrated database: seasons, boards resolved to
///     (mix, chart type), published-only session reads with the draft excluded, chart rows in
///     entry order, and the frozen board configuration deserializing with its mix pinned,
///     MaxTime/AllowRepeats read back, and the snapshot seam usable both ways (D20).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFMoMRepositoryReadTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset End = new(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-5));

    private readonly SqlServerFixture _fixture;

    public EFMoMRepositoryReadTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReadsSeasonsBoardsSessionsAndTheFrozenConfiguration()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var userId = await seeder.SeedUserAsync();
        var chartA = await seeder.SeedPhoenixChartAsync(20, "Double");
        var chartB = await seeder.SeedPhoenixChartAsync(15, "Double");

        var seasonId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var publishedId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var mixId = MixIds.For(MixEnum.Phoenix);
        var config = JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(
            new TournamentConfiguration(boardId, "Summer 2026", new ScoringConfiguration(), false,
                true)
            {
                MaxTime = TimeSpan.FromMinutes(105),
                AllowRepeats = false,
                StartDate = Start,
                EndDate = End
            }));

        await using (var ctx = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            ctx.Set<MoMSeasonEntity>().Add(new MoMSeasonEntity
            {
                Id = seasonId, Year = 2026, Quarter = 3, Name = "Summer 2026",
                StartsAt = Start, EndsAt = End, CreatedAt = Start
            });
            ctx.Set<MoMBoardEntity>().Add(new MoMBoardEntity
            {
                Id = boardId, SeasonId = seasonId, MixId = mixId,
                ChartType = (byte)ChartType.Double, ScoringConfig = config
            });
            ctx.Set<MoMSessionEntity>().Add(new MoMSessionEntity
            {
                Id = publishedId, BoardId = boardId, UserId = userId,
                PublishedAt = Start.AddDays(10), TotalScore = 2300, ChartsPlayed = 2,
                RestTime = new TimeSpan(0, 13, 43).Ticks, AverageDifficulty = 18.5,
                AverageGrade = 11.5, LowestLevel = 15, HighestLevel = 20,
                VideoUrl = "https://example.invalid/v", CreatedAt = Start.AddDays(10),
                UpdatedAt = Start.AddDays(10)
            });
            ctx.Set<MoMSessionEntity>().Add(new MoMSessionEntity
            {
                Id = draftId, BoardId = boardId, UserId = userId, PublishedAt = null,
                TotalScore = 500, ChartsPlayed = 1, RestTime = 0, AverageDifficulty = 20.5,
                AverageGrade = 9, LowestLevel = 20, HighestLevel = 20, VideoUrl = null,
                CreatedAt = Start.AddDays(11), UpdatedAt = Start.AddDays(11)
            });
            ctx.Set<MoMSessionChartEntity>().AddRange(
                new MoMSessionChartEntity
                {
                    SessionId = publishedId, Ordinal = 1, ChartId = chartB, Score = 960000,
                    Plate = "FairGame", IsBroken = true, SessionScore = 800, BonusPoints = 0,
                    PlayedAt = null
                },
                new MoMSessionChartEntity
                {
                    SessionId = publishedId, Ordinal = 0, ChartId = chartA, Score = 990000,
                    Plate = "SuperbGame", IsBroken = false, SessionScore = 1500, BonusPoints = 25,
                    PlayedAt = Start.AddDays(9)
                });
            ctx.Set<MoMChartLevelEntity>().Add(new MoMChartLevelEntity
            {
                SeasonId = seasonId, MixId = mixId, ChartId = chartA, Level = 21.5
            });
            await ctx.SaveChangesAsync();
        }

        var repository = new EFMoMRepository(_fixture.DbContextFactory,
            new MemoryCache(new MemoryCacheOptions()));

        var season = Assert.Single(await repository.GetSeasons(CancellationToken.None));
        Assert.Equal("Summer 2026", season.Name);
        Assert.Equal(2026, season.Year);
        Assert.Equal((byte)3, season.Quarter);

        var board = Assert.Single(await repository.GetBoards(CancellationToken.None));
        Assert.Equal(boardId, board.Id);
        Assert.Equal(MixEnum.Phoenix, board.Mix);
        Assert.Equal(ChartType.Double, board.ChartType);

        // Published-only: the draft never reaches a board read.
        var published = Assert.Single(await repository.GetPublishedSessions(new[] { boardId },
            CancellationToken.None));
        Assert.Equal(publishedId, published.Id);
        Assert.Equal(2300, published.TotalScore);
        Assert.Equal(new TimeSpan(0, 13, 43).Ticks, published.RestTimeTicks);

        var draft = await repository.GetDraft(boardId, userId, CancellationToken.None);
        Assert.Equal(draftId, draft?.Id);
        Assert.Null(draft!.PublishedAt);
        Assert.Null(await repository.GetDraft(boardId, Guid.NewGuid(), CancellationToken.None));

        var charts = await repository.GetSessionCharts(publishedId, CancellationToken.None);
        Assert.Equal(new[] { 0, 1 }, charts.Select(c => c.Ordinal).ToArray());
        Assert.Equal(chartA, charts[0].ChartId);
        Assert.Equal(Start.AddDays(9), charts[0].PlayedAt);

        var frozen = await repository.GetBoardConfiguration(boardId, true, CancellationToken.None);
        Assert.NotNull(frozen);
        Assert.Equal(TimeSpan.FromMinutes(105), frozen!.MaxTime);
        Assert.False(frozen.AllowRepeats);
        Assert.Equal(MixEnum.Phoenix, frozen.Scoring.Mix);
        Assert.Equal(21.5, frozen.Scoring.ChartLevelSnapshot?[chartA]);

        // The D20 seam: tables without the snapshot, and the snapshot alone.
        var bare = await repository.GetBoardConfiguration(boardId, false, CancellationToken.None);
        Assert.Null(bare!.Scoring.ChartLevelSnapshot);
        var snapshot = await repository.GetSeasonSnapshot(boardId, CancellationToken.None);
        Assert.Equal(21.5, Assert.Single(snapshot).Value);

        Assert.Null(await repository.GetBoardConfiguration(Guid.NewGuid(), true,
            CancellationToken.None));
        Assert.Null(await repository.GetSession(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DraftsUpsertPublishAndDeleteWithChartRowsCascading()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var userId = await seeder.SeedUserAsync();
        var chartA = await seeder.SeedPhoenixChartAsync(20, "Double");

        var seasonId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        await using (var ctx = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            ctx.Set<MoMSeasonEntity>().Add(new MoMSeasonEntity
            {
                Id = seasonId, Year = 2026, Quarter = 3, Name = "Summer 2026",
                StartsAt = Start, EndsAt = End, CreatedAt = Start
            });
            ctx.Set<MoMBoardEntity>().Add(new MoMBoardEntity
            {
                Id = boardId, SeasonId = seasonId, MixId = MixIds.For(MixEnum.Phoenix),
                ChartType = (byte)ChartType.Double, ScoringConfig = "{}"
            });
            await ctx.SaveChangesAsync();
        }

        var repository = new EFMoMRepository(_fixture.DbContextFactory,
            new MemoryCache(new MemoryCacheOptions()));

        // A Guid.Empty id asks storage to mint one; the draft then reads back by it.
        var draft = new MoMSessionRecord(Guid.Empty, boardId, userId, null, 1000, 1,
            TimeSpan.FromMinutes(90).Ticks, 20.5, 12, 20, 20, null);
        var rows = new[]
        {
            new MoMSessionChartRecord(0, chartA, 990000, "SuperbGame", false, 1000, 0,
                Start.AddDays(5))
        };
        var mintedId = await repository.UpsertSession(draft, rows, Start.AddDays(5),
            CancellationToken.None);
        Assert.NotEqual(Guid.Empty, mintedId);
        Assert.Equal(mintedId, (await repository.GetDraft(boardId, userId,
            CancellationToken.None))?.Id);

        // A re-save wholly replaces the chart rows rather than appending.
        var replacement = new[]
        {
            new MoMSessionChartRecord(0, chartA, 950000, "FairGame", true, 900, 0, null)
        };
        await repository.UpsertSession(draft with { Id = mintedId, TotalScore = 900 },
            replacement, Start.AddDays(6), CancellationToken.None);
        var stored = await repository.GetSessionCharts(mintedId, CancellationToken.None);
        Assert.Equal(950000, Assert.Single(stored).Score);
        Assert.Null(stored[0].PlayedAt);

        // Publish stamps the clock: the session leaves the draft read and joins the board's.
        await repository.PublishSession(mintedId, Start.AddDays(7), CancellationToken.None);
        Assert.Null(await repository.GetDraft(boardId, userId, CancellationToken.None));
        var published = Assert.Single(await repository.GetPublishedSessions(new[] { boardId },
            CancellationToken.None));
        Assert.Equal(Start.AddDays(7), published.PublishedAt);

        // Delete removes the session and its chart rows cascade with it.
        await repository.DeleteSession(mintedId, CancellationToken.None);
        Assert.Null(await repository.GetSession(mintedId, CancellationToken.None));
        Assert.Empty(await repository.GetSessionCharts(mintedId, CancellationToken.None));
    }
}
