using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ScoreHighlightRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public ScoreHighlightRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HighlightsReadBySessionRegardlessOfTheDrainSkewedTimestamp()
    {
        // A highlight is stamped at batch-drain — minutes past its journal rows — so the old
        // row-time window dropped it and the Sessions page showed nothing under "Of Note".
        // Reading by SessionId must still return it.
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var repo = new EFScoreHighlightRepository(_fixture.DbContextFactory);
        await repo.UpsertFlags(MixEnum.Phoenix, userId, new[]
        {
            new ScoreHighlightWrite(chartId, sessionId, Now.AddMinutes(3), HighlightFlags.PumbilityTop50, 24, 30.0)
        }, CancellationToken.None);

        // The old windowed read, bounded to the journal rows' minute, misses it — the bug.
        var windowed = await repo.GetHighlights(MixEnum.Phoenix, userId, Now, Now.AddMinutes(1),
            CancellationToken.None);
        Assert.Empty(windowed);

        var bySession = await repo.GetHighlightsBySessions(userId, new[] { sessionId }, CancellationToken.None);
        var hit = Assert.Single(bySession);
        Assert.Equal(chartId, hit.ChartId);
        Assert.Equal(sessionId, hit.SessionId);
        Assert.True(hit.Flags.HasFlag(HighlightFlags.PumbilityTop50));
    }

    [Fact]
    public async Task HighlightsBySessionScopeToTheRequestedSessionsAndUser()
    {
        var userId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var wantedSession = Guid.NewGuid();
        var otherSession = Guid.NewGuid();
        var repo = new EFScoreHighlightRepository(_fixture.DbContextFactory);
        await repo.UpsertFlags(MixEnum.Phoenix, userId, new[]
        {
            new ScoreHighlightWrite(Guid.NewGuid(), wantedSession, Now, HighlightFlags.ScoreQuality90, 23, 20.0),
            new ScoreHighlightWrite(Guid.NewGuid(), otherSession, Now, HighlightFlags.FolderDebut, 23, 20.0)
        }, CancellationToken.None);
        await repo.UpsertFlags(MixEnum.Phoenix, otherUser, new[]
        {
            new ScoreHighlightWrite(Guid.NewGuid(), wantedSession, Now, HighlightFlags.PumbilityTop50, 23, 20.0)
        }, CancellationToken.None);

        var result = (await repo.GetHighlightsBySessions(userId, new[] { wantedSession },
            CancellationToken.None)).ToArray();

        var hit = Assert.Single(result);
        Assert.Equal(wantedSession, hit.SessionId);
        Assert.True(hit.Flags.HasFlag(HighlightFlags.ScoreQuality90));
    }

    [Fact]
    public async Task SessionMilestonesReadBySession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var repo = new EFPlayerMilestoneRepository(_fixture.DbContextFactory);
        await repo.Append(MixEnum.Phoenix, userId, new[]
        {
            new PlayerMilestoneWrite(MilestoneKind.TitleCompleted, sessionId, Now.AddMinutes(3),
                Title: "Expert Lv. 4")
        }, CancellationToken.None);

        var bySession = await repo.GetMilestonesBySessions(userId, new[] { sessionId }, CancellationToken.None);
        var hit = Assert.Single(bySession);
        Assert.Equal(MilestoneKind.TitleCompleted, hit.Kind);
        Assert.Equal("Expert Lv. 4", hit.Title);
    }

    [Fact]
    public async Task EmptySessionSetReturnsEmpty()
    {
        var repo = new EFScoreHighlightRepository(_fixture.DbContextFactory);
        Assert.Empty(await repo.GetHighlightsBySessions(Guid.NewGuid(), Array.Empty<Guid>(),
            CancellationToken.None));
    }

    [Fact]
    public async Task NewestHighlightsFilterByFlagUserAndWindowNewestFirst()
    {
        var userId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var freshImprover = Guid.NewGuid();
        var olderImprover = Guid.NewGuid();
        var repo = new EFScoreHighlightRepository(_fixture.DbContextFactory);
        await repo.UpsertFlags(MixEnum.Phoenix, userId, new[]
        {
            // A combined-flag row still matches the improver mask.
            new ScoreHighlightWrite(freshImprover, Guid.NewGuid(), Now.AddDays(-1),
                HighlightFlags.CompetitiveImprover | HighlightFlags.PumbilityTop50, 22, 22.0),
            new ScoreHighlightWrite(olderImprover, Guid.NewGuid(), Now.AddDays(-10),
                HighlightFlags.CompetitiveImprover, 21, 21.0),
            new ScoreHighlightWrite(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-100),
                HighlightFlags.CompetitiveImprover, 21, 21.0),
            new ScoreHighlightWrite(Guid.NewGuid(), Guid.NewGuid(), Now,
                HighlightFlags.PumbilityTop50, 23, 23.0)
        }, CancellationToken.None);
        await repo.UpsertFlags(MixEnum.Phoenix, otherUser, new[]
        {
            new ScoreHighlightWrite(Guid.NewGuid(), Guid.NewGuid(), Now,
                HighlightFlags.CompetitiveImprover, 23, 23.0)
        }, CancellationToken.None);

        var result = await repo.GetNewestHighlights(MixEnum.Phoenix, userId,
            HighlightFlags.CompetitiveImprover, Now.AddDays(-30), 10, CancellationToken.None);

        Assert.Equal(new[] { freshImprover, olderImprover }, result.Select(r => r.ChartId).ToArray());
    }

    [Fact]
    public async Task NewestHighlightsCapKeepsTheNewest()
    {
        var userId = Guid.NewGuid();
        var newest = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var repo = new EFScoreHighlightRepository(_fixture.DbContextFactory);
        await repo.UpsertFlags(MixEnum.Phoenix, userId, new[]
        {
            new ScoreHighlightWrite(middle, Guid.NewGuid(), Now.AddDays(-2), HighlightFlags.CompetitiveImprover, 20, 20.0),
            new ScoreHighlightWrite(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-3), HighlightFlags.CompetitiveImprover, 20, 20.0),
            new ScoreHighlightWrite(newest, Guid.NewGuid(), Now.AddDays(-1), HighlightFlags.CompetitiveImprover, 20, 20.0)
        }, CancellationToken.None);

        var result = await repo.GetNewestHighlights(MixEnum.Phoenix, userId,
            HighlightFlags.CompetitiveImprover, null, 2, CancellationToken.None);

        Assert.Equal(new[] { newest, middle }, result.Select(r => r.ChartId).ToArray());
    }

    [Fact]
    public async Task NewestHighlightsNullSinceReachesAllTime()
    {
        var userId = Guid.NewGuid();
        var ancient = Guid.NewGuid();
        var repo = new EFScoreHighlightRepository(_fixture.DbContextFactory);
        await repo.UpsertFlags(MixEnum.Phoenix, userId, new[]
        {
            new ScoreHighlightWrite(ancient, Guid.NewGuid(), Now.AddDays(-900),
                HighlightFlags.CompetitiveImprover, 19, 19.0)
        }, CancellationToken.None);

        var windowed = await repo.GetNewestHighlights(MixEnum.Phoenix, userId,
            HighlightFlags.CompetitiveImprover, Now.AddDays(-30), 10, CancellationToken.None);
        var allTime = await repo.GetNewestHighlights(MixEnum.Phoenix, userId,
            HighlightFlags.CompetitiveImprover, null, 10, CancellationToken.None);

        Assert.Empty(windowed);
        Assert.Equal(ancient, Assert.Single(allTime).ChartId);
    }
}
