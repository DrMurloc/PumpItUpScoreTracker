using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Repositories;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Views;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFMatchRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public EFMatchRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // GetAllMatches / GetAllMatchLinks / GetMachines cache by tournamentId — fresh cache forces DB.
    // The repo serializes MatchView (and RandomSettings) as JSON; Name and PhoenixScore are value
    // types that need their JsonConverters registered or they round-trip as default(struct).
    // Program.cs registers these on the production JsonSerializerOptions; we mirror it here.
    private static System.Text.Json.JsonSerializerOptions JsonOptions()
    {
        var opts = new System.Text.Json.JsonSerializerOptions();
        opts.Converters.Add(Name.Converter);
        opts.Converters.Add(PhoenixScore.Converter);
        return opts;
    }

    private EFMatchRepository BuildRepository() =>
        new(_fixture.DbContextFactory,
            Options.Create(JsonOptions()),
            new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task SaveMatchAndGetMatchRoundTripPreservesTheSerializedView()
    {
        // MatchView is persisted as a JSON blob keyed by (TournamentId, MatchName). Round-trip
        // catches any serializer-config drift or schema change to the underlying JSON column.
        var tournamentId = Guid.NewGuid();
        var match = NewMatchView("Quarter 1", "Quarter Finals", order: 1);

        await BuildRepository().SaveMatch(tournamentId, match, CancellationToken.None);

        var retrieved = await BuildRepository().GetMatch(tournamentId, "Quarter 1", CancellationToken.None);

        Assert.Equal("Quarter 1", (string)retrieved.MatchName);
        Assert.Equal("Quarter Finals", (string)retrieved.PhaseName);
        Assert.Equal(1, retrieved.MatchOrder);
        Assert.Equal(MatchState.NotStarted, retrieved.State);
        Assert.Equal(2, retrieved.Players.Length);
    }

    [Fact]
    public async Task GetAllMatchesReturnsAllMatchesForOneTournamentNotOthers()
    {
        var tournamentA = Guid.NewGuid();
        var tournamentB = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveMatch(tournamentA, NewMatchView("Quarter 1", "Phase", 1), CancellationToken.None);
        await writer.SaveMatch(tournamentA, NewMatchView("Quarter 2", "Phase", 2), CancellationToken.None);
        await writer.SaveMatch(tournamentB, NewMatchView("Quarter 1", "Phase", 1), CancellationToken.None);

        var aMatches = (await BuildRepository().GetAllMatches(tournamentA, CancellationToken.None)).ToList();

        Assert.Equal(2, aMatches.Count);
        Assert.Contains(aMatches, m => (string)m.MatchName == "Quarter 1");
        Assert.Contains(aMatches, m => (string)m.MatchName == "Quarter 2");
    }

    [Fact]
    public async Task SaveMatchLinkAndGetAllMatchLinksRoundTrip()
    {
        var tournamentId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var link = new MatchLink(linkId, FromMatch: "Quarter 1", ToMatch: "Semi 1",
            IsWinners: true, PlayerCount: 2, Skip: 0);

        await BuildRepository().SaveMatchLink(tournamentId, link, CancellationToken.None);

        var links = (await BuildRepository().GetAllMatchLinks(tournamentId, CancellationToken.None)).ToList();

        Assert.Single(links);
        Assert.Equal(linkId, links[0].Id);
        Assert.Equal("Quarter 1", (string)links[0].FromMatch);
        Assert.Equal("Semi 1", (string)links[0].ToMatch);
        Assert.True(links[0].IsWinners);
        Assert.Equal(2, links[0].PlayerCount);
    }

    [Fact]
    public async Task GetMatchLinksByFromMatchNameFiltersByFromMatch()
    {
        var tournamentId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveMatchLink(tournamentId,
            new MatchLink(Guid.NewGuid(), "Quarter 1", "Semi 1", true, 2, 0), CancellationToken.None);
        await writer.SaveMatchLink(tournamentId,
            new MatchLink(Guid.NewGuid(), "Quarter 2", "Semi 1", true, 2, 0), CancellationToken.None);

        var fromQ1 = (await BuildRepository()
            .GetMatchLinksByFromMatchName(tournamentId, "Quarter 1", CancellationToken.None)).ToList();

        Assert.Single(fromQ1);
        Assert.Equal("Quarter 1", (string)fromQ1[0].FromMatch);
    }

    [Fact]
    public async Task DeleteMatchLinkRemovesTheLink()
    {
        var tournamentId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveMatchLink(tournamentId,
            new MatchLink(linkId, "Quarter 1", "Semi 1", true, 2, 0), CancellationToken.None);

        await writer.DeleteMatchLink(linkId, CancellationToken.None);

        var links = (await BuildRepository().GetAllMatchLinks(tournamentId, CancellationToken.None)).ToList();

        Assert.Empty(links);
    }

    [Fact]
    public async Task SaveMatchPlayerAndGetMatchPlayersRoundTrip()
    {
        var tournamentId = Guid.NewGuid();
        var player = new MatchPlayer("Alice", Seed: 1, DiscordId: 12345UL, Notes: "test note",
            PotentialConflict: true);

        await BuildRepository().SaveMatchPlayer(tournamentId, player, CancellationToken.None);

        var players = (await BuildRepository().GetMatchPlayers(tournamentId, CancellationToken.None)).ToList();

        Assert.Single(players);
        Assert.Equal("Alice", (string)players[0].Name);
        Assert.Equal(1, players[0].Seed);
        Assert.Equal(12345UL, players[0].DiscordId);
        Assert.Equal("test note", players[0].Notes);
        Assert.True(players[0].PotentialConflict);
    }

    [Fact]
    public async Task SaveMachineAndGetMachinesRoundTrip()
    {
        var tournamentId = Guid.NewGuid();
        var machine = new MatchMachineRecord("Cab 1", Priority: 10, IsWarmup: false);

        await BuildRepository().SaveMachine(tournamentId, machine, CancellationToken.None);

        var machines = (await BuildRepository().GetMachines(tournamentId, CancellationToken.None)).ToList();

        Assert.Single(machines);
        Assert.Equal("Cab 1", (string)machines[0].MachineName);
        Assert.Equal(10, machines[0].Priority);
        Assert.False(machines[0].IsWarmup);
    }

    private static MatchView NewMatchView(Name matchName, Name phaseName, int order) =>
        new(MatchName: matchName,
            PhaseName: phaseName,
            MatchOrder: order,
            ChartCount: 3,
            RandomSettings: "default",
            State: MatchState.NotStarted,
            Players: new Name[] { "Alice", "Bob" },
            ActiveCharts: Array.Empty<Guid>(),
            VetoedCharts: Array.Empty<Guid>(),
            ProtectedCharts: Array.Empty<Guid>(),
            Scores: new Dictionary<string, PhoenixScore[]>(),
            Points: new Dictionary<string, int[]>(),
            FinalPlaces: Array.Empty<Name>());
}
