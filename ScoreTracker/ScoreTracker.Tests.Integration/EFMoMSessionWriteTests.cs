using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Infrastructure;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The session lifecycle against a real migrated database (docs/design/march-of-murlocs.md
///     §11.4): a draft is created with no publication stamp, its derived cache columns are
///     recomputed from the chart rows on every save (§6), publishing stamps it once, and deleting
///     takes the chart rows with it. The mocked handler suite cannot catch a column that never
///     lands — this can.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFMoMSessionWriteTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly SqlServerFixture _fixture;
    private Guid _boardId;
    private Guid _userId;
    private TournamentConfiguration _configuration = null!;

    public EFMoMSessionWriteTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        _userId = await seeder.SeedUserAsync("DRMURLOC");
        (_boardId, _configuration) = await SeedBoard();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private EFMoMRepository Repo() => new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()),
        Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Now));

    private async Task<(Guid BoardId, TournamentConfiguration Configuration)> SeedBoard()
    {
        var seasonId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var scoring = ScoringConfiguration.PumbilityPlus;
        scoring.AdjustToTime = true;
        var configuration = new TournamentConfiguration(boardId, "frozen", scoring, false, true)
            { MaxTime = TimeSpan.FromMinutes(105), AllowRepeats = false };

        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.MoMSeason (Id, [Year], Quarter, Name, StartsAt, EndsAt, CreatedAt) VALUES ({0}, 2026, 3, {1}, {2}, {3}, {2})",
            seasonId, "Summer 2026", Start, Start.AddMonths(3));
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.MoMBoard (Id, SeasonId, MixId, ChartType, ScoringConfig) VALUES ({0}, {1}, {2}, {3}, {4})",
            boardId, seasonId, MixIds.Phoenix, (byte)ChartType.Double,
            JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(configuration)));
        return (boardId, configuration);
    }

    /// <summary>A catalog chart, and the SharedKernel model that matches it, for the aggregate.</summary>
    private async Task<Chart> Chart(string name, int level, int seconds)
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var id = await seeder.SeedPhoenixChartAsync(level, "Double");
        var song = new Song(Name.From(name), SongType.Arcade, new Uri("https://example.invalid/s.png"),
            TimeSpan.FromSeconds(seconds), Name.From("artist"), null);
        return new Chart(id, MixEnum.Phoenix, song, ChartType.Double, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null);
    }

    private TournamentSession Session() => new(_userId, _configuration, MixEnum.Phoenix);

    private async Task<MoMSessionEntity> Stored(Guid sessionId)
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        return await ctx.Set<MoMSessionEntity>().SingleAsync(s => s.Id == sessionId);
    }

    private async Task<MoMSessionChartEntity[]> Rows(Guid sessionId)
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        return await ctx.Set<MoMSessionChartEntity>().Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.Ordinal).ToArrayAsync();
    }

    [Fact]
    public async Task AnEmptyDraftIsStoredWithNoPublicationStampAndIsFoundAgainAsTheOpenDraft()
    {
        var repo = Repo();
        var sessionId = Guid.NewGuid();

        await repo.SaveSession(sessionId, Session(), CancellationToken.None);

        var stored = await Stored(sessionId);
        Assert.Null(stored.PublishedAt);
        Assert.Equal(0, stored.ChartsPlayed);
        Assert.Equal(0, stored.TotalScore);
        Assert.Equal(Now, stored.CreatedAt);
        Assert.Empty(await Rows(sessionId));
        Assert.Equal(sessionId, await repo.GetDraftId(_boardId, _userId, CancellationToken.None));
    }

    [Fact]
    public async Task SavingRecomputesEveryDerivedColumnFromTheChartRows()
    {
        var repo = Repo();
        var sessionId = Guid.NewGuid();
        var session = Session();
        var gargoyle = await Chart("Gargoyle", 20, 115);
        var uglyDee = await Chart("Ugly Dee", 17, 96);
        var playedAt = Now.AddHours(-1);
        session.Add(gargoyle, 986121, PhoenixPlate.MarvelousGame, false, playedAt);
        session.Add(uglyDee, 970915, PhoenixPlate.MarvelousGame, false, playedAt.AddMinutes(3));

        await repo.SaveSession(sessionId, session, CancellationToken.None);

        var stored = await Stored(sessionId);
        Assert.Equal(2, stored.ChartsPlayed);
        Assert.Equal(session.TotalScore, stored.TotalScore);
        Assert.Equal(session.CurrentRestTime.Ticks, stored.RestTime);
        // No season snapshot rows, so balanced level is folder + 0.5 for both (§9.3).
        Assert.Equal(19.0, stored.AverageDifficulty, 3);
        Assert.Equal((byte)17, stored.LowestLevel);
        Assert.Equal((byte)20, stored.HighestLevel);

        var rows = await Rows(sessionId);
        Assert.Equal(new[] { gargoyle.Id, uglyDee.Id }, rows.Select(r => r.ChartId));
        Assert.Equal(playedAt, rows[0].PlayedAt);
        Assert.Equal(playedAt.AddMinutes(3), rows[1].PlayedAt);
        Assert.Equal(970915, rows[1].Score);
    }

    [Fact]
    public async Task SavingAgainReplacesTheChartRowsRatherThanAppendingToThem()
    {
        var repo = Repo();
        var sessionId = Guid.NewGuid();
        var session = Session();
        var gargoyle = await Chart("Gargoyle", 20, 115);
        session.Add(gargoyle, 986121, PhoenixPlate.MarvelousGame, false);
        await repo.SaveSession(sessionId, session, CancellationToken.None);

        session.Remove(session.Entries.Single());
        session.Add(await Chart("Slam", 24, 128), 980000, PhoenixPlate.MarvelousGame, false);
        await repo.SaveSession(sessionId, session, CancellationToken.None);

        var rows = await Rows(sessionId);
        Assert.Single(rows);
        Assert.Equal(0, rows[0].Ordinal);
        Assert.Equal((byte)24, (await Stored(sessionId)).HighestLevel);
    }

    [Fact]
    public async Task PublishingStampsItOnceAndTheDraftLookupStopsFindingIt()
    {
        var repo = Repo();
        var sessionId = Guid.NewGuid();
        var session = Session();
        session.Add(await Chart("Slam", 24, 128), 980000, PhoenixPlate.MarvelousGame, false);
        await repo.SaveSession(sessionId, session, CancellationToken.None);

        await repo.PublishSession(sessionId, Now, CancellationToken.None);
        await repo.PublishSession(sessionId, Now.AddDays(1), CancellationToken.None);

        // The earliest publication wins a tie (§1), so publishing twice must not move it.
        Assert.Equal(Now, (await Stored(sessionId)).PublishedAt);
        Assert.Null(await repo.GetDraftId(_boardId, _userId, CancellationToken.None));
    }

    [Fact]
    public async Task SavingAPublishedSessionKeepsItsStampAndItsCreationTime()
    {
        var repo = Repo();
        var sessionId = Guid.NewGuid();
        var session = Session();
        session.Add(await Chart("Slam", 24, 128), 980000, PhoenixPlate.MarvelousGame, false);
        await repo.SaveSession(sessionId, session, CancellationToken.None);
        var created = (await Stored(sessionId)).CreatedAt;
        await repo.PublishSession(sessionId, Now, CancellationToken.None);

        await repo.SaveSession(sessionId, session, CancellationToken.None);

        var stored = await Stored(sessionId);
        Assert.Equal(Now, stored.PublishedAt);
        Assert.Equal(created, stored.CreatedAt);
    }

    [Fact]
    public async Task DeletingTakesTheChartRowsWithIt()
    {
        var repo = Repo();
        var sessionId = Guid.NewGuid();
        var session = Session();
        session.Add(await Chart("Slam", 24, 128), 980000, PhoenixPlate.MarvelousGame, false);
        await repo.SaveSession(sessionId, session, CancellationToken.None);

        await repo.DeleteSession(sessionId, CancellationToken.None);

        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.False(await ctx.Set<MoMSessionEntity>().AnyAsync(s => s.Id == sessionId));
        Assert.False(await ctx.Set<MoMSessionChartEntity>().AnyAsync(c => c.SessionId == sessionId));
    }

    [Fact]
    public async Task DeletingSomethingThatIsNotThereIsQuiet()
    {
        await Repo().DeleteSession(Guid.NewGuid(), CancellationToken.None);
        await Repo().PublishSession(Guid.NewGuid(), Now, CancellationToken.None);
        Assert.Null(await Repo().GetDraftId(_boardId, _userId, CancellationToken.None));
    }
}
