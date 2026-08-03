using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The completeness check's ledger against a real schema: findings survive the round trip,
///     the panel reads back the newest run for its mix, and the monthly deep-scan count is a
///     calendar month rather than a rolling window.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFImportCheckRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset August = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
    private readonly SqlServerFixture _fixture;

    public EFImportCheckRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private EFImportCheckRepository Repository()
    {
        return new EFImportCheckRepository(_fixture.DbContextFactory);
    }

    [Fact]
    public async Task FindingsSurviveTheRoundTrip()
    {
        var repository = Repository();
        var userId = Guid.NewGuid();
        await repository.Save(Run(userId, August, ImportCheckKind.Census, new[]
        {
            new CensusFinding("18", CensusFindingKind.Missing, 2),
            new CensusFinding("21", CensusFindingKind.OutOfDate, 1, "AA", true)
        }), CancellationToken.None);

        var stored = await repository.GetLatest(userId, MixEnum.Phoenix, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(2, stored!.Findings.Count);
        var stale = stored.Findings.Single(f => f.Kind == CensusFindingKind.OutOfDate);
        Assert.Equal("21", stale.Bucket);
        Assert.Equal("AA", stale.Band);
        Assert.True(stale.IsGradeBand);
        Assert.Equal(64466, stored.OfficialPumbility);
    }

    [Fact]
    public async Task ThePanelReadsTheNewestRunForItsOwnMix()
    {
        var repository = Repository();
        var userId = Guid.NewGuid();
        await repository.Save(Run(userId, August, ImportCheckKind.Census, Array.Empty<CensusFinding>()),
            CancellationToken.None);
        await repository.Save(
            Run(userId, August.AddHours(1), ImportCheckKind.Census,
                new[] { new CensusFinding("18", CensusFindingKind.Missing, 1) }), CancellationToken.None);
        await repository.Save(
            Run(userId, August.AddHours(2), ImportCheckKind.Census,
                new[] { new CensusFinding("9", CensusFindingKind.Missing, 5) }, MixEnum.Phoenix2),
            CancellationToken.None);

        var phoenix = await repository.GetLatest(userId, MixEnum.Phoenix, CancellationToken.None);
        var phoenix2 = await repository.GetLatest(userId, MixEnum.Phoenix2, CancellationToken.None);

        // The newer Phoenix 2 run must not become Phoenix's standing verdict.
        Assert.Equal("18", phoenix!.Findings.Single().Bucket);
        Assert.Equal("9", phoenix2!.Findings.Single().Bucket);
    }

    [Fact]
    public async Task NoRunsMeansNoStandingVerdict()
    {
        Assert.Null(await Repository().GetLatest(Guid.NewGuid(), MixEnum.Phoenix, CancellationToken.None));
    }

    [Fact]
    public async Task OnlyDeepScansInThisCalendarMonthCountAgainstTheLimit()
    {
        var repository = Repository();
        var userId = Guid.NewGuid();
        await repository.Save(Run(userId, August, ImportCheckKind.Deep, Array.Empty<CensusFinding>()),
            CancellationToken.None);
        // Both mixes draw on one allowance — the limit protects piugame, and both mixes are it.
        await repository.Save(
            Run(userId, August.AddDays(1), ImportCheckKind.Deep, Array.Empty<CensusFinding>(), MixEnum.Phoenix2),
            CancellationToken.None);
        // A census run is free and never counts.
        await repository.Save(Run(userId, August.AddDays(2), ImportCheckKind.Census, Array.Empty<CensusFinding>()),
            CancellationToken.None);
        // Last month's scans are spent and gone — a calendar month, not a rolling window.
        await repository.Save(Run(userId, August.AddMonths(-1), ImportCheckKind.Deep, Array.Empty<CensusFinding>()),
            CancellationToken.None);

        Assert.Equal(2, await repository.CountDeepScansInMonth(userId, August, CancellationToken.None));
        Assert.Equal(1, await repository.CountDeepScansInMonth(userId, August.AddMonths(-1), CancellationToken.None));
    }

    [Fact]
    public async Task AnotherPlayersScansDoNotSpendYours()
    {
        var repository = Repository();
        var mine = Guid.NewGuid();
        await repository.Save(Run(Guid.NewGuid(), August, ImportCheckKind.Deep, Array.Empty<CensusFinding>()),
            CancellationToken.None);

        Assert.Equal(0, await repository.CountDeepScansInMonth(mine, August, CancellationToken.None));
    }

    private static ImportCheckRun Run(Guid userId, DateTimeOffset at, ImportCheckKind kind,
        IReadOnlyList<CensusFinding> findings, MixEnum mix = MixEnum.Phoenix)
    {
        return new ImportCheckRun(Guid.NewGuid(), userId, mix, at, kind, 64466, 64466, 2851, 2851, findings);
    }
}
