using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Catalog;

/// <summary>
///     What a folder-scoped score projection actually costs, against a real populated database.
///     This is the measurement the personalized Score tier list is gated on: the PUMBILITY page
///     takes seconds for its sweep, and the whole argument for computing the tier list's version
///     on demand is that a folder is a far smaller question — one chart type instead of two, one
///     level instead of a five-level band, and half the cohort at ±0.5 instead of ±1.0.
///     <para>
///         If that argument is right, the answer here is well under a second and the blend's
///         existing 6h/1h cache is all the machinery needed. If it is wrong, projected scores
///         want materializing per competitive-level bucket instead, which is tractable precisely
///         because the estimate depends on the player only through their level.
///     </para>
///     <para>
///         It also reports coverage, which decides two things: whether the source is worth
///         having at all in folders away from a player's level, and where
///         <c>MinProjectedCharts</c> belongs — below that floor the tier bands are cut from too
///         little spread to mean anything.
///     </para>
///     <para>
///         Needs a populated database, which CI does not have:
///         <c>dotnet user-secrets set "CatalogProbe:ConnectionString" "..." --project ScoreTracker/ScoreTracker.AppHost</c>
///         then
///         <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj --filter "FullyQualifiedName~ScoreProjectionCostProbe"</c>.
///         Read-only: it resolves the real ports and SELECTs through them.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ScoreProjectionCostProbeTests
{
    /// <summary>
    ///     The window the tier list asks for. Stated here rather than imported because the probe
    ///     lands before the lens that uses it — when TierListBlendBuilder grows its own constant,
    ///     the two must agree or this measures something the page does not do.
    /// </summary>
    private const double TierListWindow = 0.5;

    /// <summary>Levels sampled across the competitive range the site actually has players at.</summary>
    private static readonly int[] AnchorLevels = { 17, 19, 21, 23 };

    /// <summary>How far above their own level a player's folder is also sampled — browsing up is
    ///     the case where peers thin out, and the one most likely to come back empty.</summary>
    private static readonly int[] FolderOffsets = { 0, 2 };

    private const int PlayersPerLevel = 3;

    private readonly ITestOutputHelper _output;

    public ScoreProjectionCostProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task A_folder_scoped_projection_is_cheap_enough_to_compute_on_demand()
    {
        await using var services = BuildPorts();
        var projector = services.GetRequiredService<IScoreProjector>();
        var charts = services.GetRequiredService<IChartRepository>();
        var stats = services.GetRequiredService<IPlayerStatsReader>();

        var runs = new List<Run>();
        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        foreach (var anchor in AnchorLevels)
        {
            // Players sitting essentially AT the anchor, so the folder offsets below mean what
            // they say. A quarter-level window keeps the sample honest without needing the
            // stats table's shape.
            var players = (await stats.GetPlayersByCompetitiveRange(MixEnum.Phoenix, chartType, anchor, 0.25,
                CancellationToken.None)).Take(PlayersPerLevel).ToArray();
            if (players.Length == 0)
            {
                _output.WriteLine($"{chartType} {anchor}: no players at this level, skipped.");
                continue;
            }

            foreach (var offset in FolderOffsets)
            {
                var level = anchor + offset;
                if (level > DifficultyLevel.Max) continue;
                var folder = (await charts.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(level), chartType,
                    cancellationToken: CancellationToken.None)).ToArray();
                if (folder.Length == 0) continue;

                var targets = folder.Select(c => new ProjectionTarget(c.Id, (int)c.Level)).ToArray();
                foreach (var player in players)
                    runs.Add(await Measure(projector, chartType, player, anchor, level, targets, TierListWindow));
            }
        }

        Assert.True(runs.Count > 0,
            "No (player, folder) pair could be sampled — the configured database is not populated " +
            "enough for this probe to mean anything. Point CatalogProbe:ConnectionString at a " +
            "prod-synced database.");

        Report("folder-scoped, ±" + TierListWindow, runs);

        // The comparison that justifies the narrower window: the same folders at PUMBILITY's
        // ±1.0, which is roughly twice the cohort and therefore twice the peer scores to read.
        var atOne = new List<Run>();
        foreach (var run in runs.Where(r => r.Offset == 0))
        {
            var folder = (await charts.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(run.Level), run.ChartType,
                cancellationToken: CancellationToken.None)).ToArray();
            atOne.Add(await Measure(projector, run.ChartType, run.UserId, run.Anchor, run.Level,
                folder.Select(c => new ProjectionTarget(c.Id, (int)c.Level)).ToArray(),
                CohortEstimator.CompetitiveWindow));
        }

        if (atOne.Count > 0) Report("same folders, ±" + CohortEstimator.CompetitiveWindow, atOne);

        // The one hard assertion. Timing is reported rather than asserted — a wall clock on one
        // machine is not a gate — but a projection that reaches nothing in a player's OWN folder
        // means the source can never vote, and the lens change should not land on top of that.
        var ownFolder = runs.Where(r => r.Offset == 0).ToArray();
        Assert.True(ownFolder.Any(r => r.Projected >= 3),
            "No player got a projection covering three or more charts in their own folder, so the " +
            "Score lens would be silent for everyone. Either the cohort read is broken or ±" +
            TierListWindow + " is too narrow for this population.");
    }

    private static async Task<Run> Measure(IScoreProjector projector, ChartType chartType, Guid userId,
        int anchor, int level, IReadOnlyCollection<ProjectionTarget> targets, double window)
    {
        var clock = Stopwatch.StartNew();
        var projected = await projector.Project(
            new ScoreProjectionRequest(MixEnum.Phoenix, chartType, userId, targets, window),
            CancellationToken.None);
        clock.Stop();
        return new Run(userId, chartType, anchor, level, level - anchor, targets.Count, projected.Count,
            clock.ElapsedMilliseconds);
    }

    private void Report(string label, IReadOnlyList<Run> runs)
    {
        _output.WriteLine("");
        _output.WriteLine($"── {label} — {runs.Count} runs ──");
        _output.WriteLine("type   anchor  folder  charts  projected  coverage    ms");
        foreach (var r in runs.OrderBy(r => r.ChartType).ThenBy(r => r.Anchor).ThenBy(r => r.Level))
            _output.WriteLine(
                $"{r.ChartType,-7}{r.Anchor,-8}{r.Level,-8}{r.Charts,-8}{r.Projected,-11}" +
                $"{r.Projected / (double)Math.Max(1, r.Charts),-12:P0}{r.Milliseconds,6}");

        var times = runs.Select(r => r.Milliseconds).OrderBy(m => m).ToArray();
        _output.WriteLine(
            $"median {times[times.Length / 2]}ms · p95 {times[(int)(times.Length * 0.95) % times.Length]}ms · " +
            $"max {times[^1]}ms · mean coverage " +
            $"{runs.Average(r => r.Projected / (double)Math.Max(1, r.Charts)):P0}");

        foreach (var group in runs.GroupBy(r => r.Offset).OrderBy(g => g.Key))
            _output.WriteLine(
                $"  folder {(group.Key >= 0 ? "+" : "")}{group.Key}: mean coverage " +
                $"{group.Average(r => r.Projected / (double)Math.Max(1, r.Charts)):P0} · " +
                $"{group.Count(r => r.Projected < 3)}/{group.Count()} runs under the 3-chart floor");
    }

    private sealed record Run(Guid UserId, ChartType ChartType, int Anchor, int Level, int Offset,
        int Charts, int Projected, long Milliseconds);

    /// <summary>
    ///     The production port graph without the web host. AddInfrastructure binds every
    ///     Domain.SecondaryPorts interface to its real implementation and registers the verticals;
    ///     IScoreProjector is registered here for the same reason Program.cs does it by hand — it
    ///     is a Domain service, so the reflection over ScoreTracker.Data never sees it.
    /// </summary>
    private static ServiceProvider BuildPorts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(CatalogRegistrationExtensions).Assembly,
            typeof(ChartIntelligenceRegistrationExtensions).Assembly,
            typeof(ScoreLedgerRegistrationExtensions).Assembly,
            typeof(PlayerProgressRegistrationExtensions).Assembly,
            typeof(IdentityRegistrationExtensions).Assembly));
        services.AddInfrastructure(new AzureBlobConfiguration(),
            new SqlConfiguration { ConnectionString = CatalogProbeConfiguration.ConnectionString! },
            new SendGridConfiguration());
        services.AddTransient<IScoreProjector, ScoreProjector>();
        return services.BuildServiceProvider();
    }
}
