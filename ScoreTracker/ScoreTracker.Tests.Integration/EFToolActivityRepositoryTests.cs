using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The hourly roll-up against a real database.
///     <para>
///         The upsert's identity is tool, kind, hour and detail. Whether a null detail matches a
///         null column, and whether two details in one hour stay two rows, is decided by the SQL
///         the provider generates — which is exactly what a mocked repository cannot say.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFToolActivityRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 15, 0, TimeSpan.Zero);
    private static readonly Guid AToolId = Guid.Parse("eeeeeeee-0000-0000-0000-00000000000e");

    private readonly SqlServerFixture _fixture;

    public EFToolActivityRepositoryTests(SqlServerFixture fixture)
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

    private EFToolActivityRepository Repository => new(_fixture.DbContextFactory);

    private Task<IReadOnlyList<ToolActivityRecord>> Rows => Repository.GetRecent(AToolId, 10);

    /// <summary>
    ///     Two live keys per tool is the rotation story, so a maker rolling keys in the same hour
    ///     must see each one's traffic under its own name.
    /// </summary>
    [Fact]
    public async Task TwoKeysInOneHourAreTwoRows()
    {
        await Repository.Increment(AToolId, ToolActivityKind.KeyUsed, Now, "production");
        await Repository.Increment(AToolId, ToolActivityKind.KeyUsed, Now.AddMinutes(10), "staging");

        var rows = await Rows;

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(1, r.Count));
        Assert.Equal(new[] { "production", "staging" }, rows.Select(r => r.Detail).OrderBy(d => d));
    }

    [Fact]
    public async Task OneKeyTwiceInOneHourIsOneRowCountingTwo()
    {
        await Repository.Increment(AToolId, ToolActivityKind.KeyUsed, Now, "production");
        await Repository.Increment(AToolId, ToolActivityKind.KeyUsed, Now.AddMinutes(10), "production");

        var row = Assert.Single(await Rows);

        Assert.Equal(2, row.Count);
        Assert.Equal("production", row.Detail);
        Assert.Equal(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero), row.WindowStart);
        Assert.Equal(Now.AddMinutes(10), row.OccurredAt);
    }

    /// <summary>The directory-click roll-up carries no detail and must keep folding as it always has.</summary>
    [Fact]
    public async Task ANullDetailStillFoldsIntoTheHour()
    {
        await Repository.Increment(AToolId, ToolActivityKind.DirectoryClicked, Now);
        await Repository.Increment(AToolId, ToolActivityKind.DirectoryClicked, Now.AddMinutes(1));

        var row = Assert.Single(await Rows);

        Assert.Equal(2, row.Count);
        Assert.Null(row.Detail);
    }

    [Fact]
    public async Task TheNextHourStartsANewRow()
    {
        await Repository.Increment(AToolId, ToolActivityKind.KeyUsed, Now, "production");
        await Repository.Increment(AToolId, ToolActivityKind.KeyUsed, Now.AddHours(1), "production");

        Assert.Equal(2, (await Rows).Count);
    }

    /// <summary>A different kind under the same name is a different tally.</summary>
    [Fact]
    public async Task KindsDoNotShareARow()
    {
        await Repository.Increment(AToolId, ToolActivityKind.KeyUsed, Now, "production");
        await Repository.Increment(AToolId, ToolActivityKind.RateLimited, Now, "production");

        var rows = await Rows;

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Kind == ToolActivityKind.KeyUsed);
        Assert.Contains(rows, r => r.Kind == ToolActivityKind.RateLimited);
    }
}
