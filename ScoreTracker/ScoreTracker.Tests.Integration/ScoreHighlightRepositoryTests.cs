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
}
