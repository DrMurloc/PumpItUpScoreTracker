using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Real-SQL coverage for who is on the Hardmode board (docs/design/hardmode-leaderboard.md
///     D17). A visibility rule that lives in a WHERE clause can only be proven against an engine
///     that runs it: the mocked suites see the handler pass a viewer id through and nothing else,
///     and the shipped board had no filter at all while every test above it passed.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFHardmodeRatingRepositoryTests : IAsyncLifetime
{
    private const MixEnum Mix = MixEnum.Phoenix2;

    private readonly SqlServerFixture _fixture;

    public EFHardmodeRatingRepositoryTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFHardmodeRatingRepository BuildRepository() =>
        new(_fixture.DbContextFactory, NullLogger<EFHardmodeRatingRepository>.Instance);

    /// <summary>
    ///     An account with a stats row on the mix and a Hardmode total, public or not. The rating
    ///     write only touches rows that already exist, so the stats row has to come first.
    /// </summary>
    private async Task<Guid> SeedRated(bool isPublic, double combined, double singles = 0, double doubles = 0)
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var userId = await seeder.SeedUserAsync(isPublic: isPublic);
        var stats = new EFPlayerStatsRepository(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));
        await stats.SaveStats(Mix, userId,
            new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            CancellationToken.None);
        await BuildRepository().Save(Mix,
            new[] { new HardmodeRatingRow(userId, combined, singles, doubles, 10, 5, 5) },
            CancellationToken.None);
        return userId;
    }

    [Fact]
    public async Task APrivateAccountIsOffAStrangersBoardAndIsCountedInstead()
    {
        var top = await SeedRated(true, 17_000);
        var hidden = await SeedRated(false, 16_000);
        var alsoHidden = await SeedRated(false, 15_500);
        var viewer = await SeedRated(true, 15_000);

        var board = await BuildRepository().GetBoard(Mix, null, viewer, CancellationToken.None);

        // Not anonymised, not greyed - absent. The count is the only trace, so the viewer can see
        // the field is smaller than the population rather than reading a filtered board as whole.
        Assert.Equal(new[] { top, viewer }, board.Rows.Select(r => r.UserId));
        Assert.DoesNotContain(board.Rows, r => r.UserId == hidden || r.UserId == alsoHidden);

        // Places number over what came back: the public runner-up is #2, not #4 with two gaps.
        Assert.Equal(new[] { 1, 2 }, board.Rows.Select(r => r.Place));
    }

    [Fact]
    public async Task APrivateAccountIsOnItsOwnBoardAndNobodyElseIsHiddenFromIt()
    {
        var top = await SeedRated(true, 17_000);
        var me = await SeedRated(false, 16_000);
        var stranger = await SeedRated(false, 15_500);
        var below = await SeedRated(true, 15_000);

        var board = await BuildRepository().GetBoard(Mix, null, me, CancellationToken.None);

        // The exception the rule is built around: a board that hides you from yourself cannot
        // answer where you stand, which is the question this page exists to answer.
        Assert.Equal(new[] { top, me, below }, board.Rows.Select(r => r.UserId));
        Assert.Equal(2, board.Rows.Single(r => r.UserId == me).Place);
        Assert.DoesNotContain(board.Rows, r => r.UserId == stranger);
    }

    [Fact]
    public async Task AnAnonymousReadGetsThePublicBoardAndTheFullHiddenCount()
    {
        var top = await SeedRated(true, 17_000);
        await SeedRated(false, 16_000);
        await SeedRated(false, 15_500);

        var board = await BuildRepository().GetBoard(Mix, null, null, CancellationToken.None);

        // Guid.Empty stands in for "nobody" in the read. Compared against a nullable instead,
        // SQL's `stats.UserId = NULL` is unknown rather than false, so the "or it is your own
        // row" arm never fires - invisible for an anonymous read, which has no own row, and the
        // reason the substitution is made at the top of the method rather than per-arm.
        Assert.Equal(new[] { top }, board.Rows.Select(r => r.UserId));
    }

    [Fact]
    public async Task TheStandingIsCountedOverTheSameFieldTheBoardShows()
    {
        await SeedRated(true, 17_000);
        await SeedRated(false, 16_500);
        await SeedRated(false, 16_200);
        var viewer = await SeedRated(true, 16_000);
        await SeedRated(true, 15_000);

        var repository = BuildRepository();
        var board = await repository.GetBoard(Mix, null, viewer, CancellationToken.None);
        var standing = await repository.GetStanding(Mix, null, viewer, CancellationToken.None);

        // "#2 of 3" over three printed rows. Counted over everyone instead it would read "#4 of
        // 5" above a list of three, which is how the leak showed up on the page before the rows.
        Assert.NotNull(standing);
        Assert.Equal(2, standing!.Value.Place);
        Assert.Equal(3, standing.Value.Field);
        Assert.Equal(board.Rows.Count, standing.Value.Field);
        Assert.Equal(board.Rows.Single(r => r.UserId == viewer).Place, standing.Value.Place);
    }

    [Fact]
    public async Task APrivateViewerKeepsTheirOwnStandingInTheFieldTheySee()
    {
        await SeedRated(true, 17_000);
        await SeedRated(false, 16_500);
        var me = await SeedRated(false, 16_000);
        await SeedRated(true, 15_000);

        var repository = BuildRepository();
        var standing = await repository.GetStanding(Mix, null, me, CancellationToken.None);
        var board = await repository.GetBoard(Mix, null, me, CancellationToken.None);

        // Their own row is in the field, the other private account is not: two public accounts
        // plus themselves.
        Assert.NotNull(standing);
        Assert.Equal(2, standing!.Value.Place);
        Assert.Equal(3, standing.Value.Field);
        Assert.Equal(board.Rows.Count, standing.Value.Field);
    }

    [Fact]
    public async Task EachPoolFiltersOnItsOwnTotal()
    {
        var doublesOnly = await SeedRated(true, 9_000, 0, 9_000);
        await SeedRated(false, 12_000, 12_000);
        var viewer = await SeedRated(true, 8_000, 8_000);

        var repository = BuildRepository();
        var singles = await repository.GetBoard(Mix, ChartType.Single, viewer, CancellationToken.None);
        var doubles = await repository.GetBoard(Mix, ChartType.Double, viewer, CancellationToken.None);

        // A zero is not a standing, so the doubles-only account is off the singles board entirely
        // - and the private singles account is off it for the other reason, counted rather than
        // listed. The two exclusions are different facts and must not be confused for each other.
        Assert.Equal(new[] { viewer }, singles.Rows.Select(r => r.UserId));
        Assert.Equal(new[] { doublesOnly }, doubles.Rows.Select(r => r.UserId));
    }
}
