using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ExplorationTests.Catalog;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Pumbility;

/// <summary>
///     The census behind the archetype cutoffs (docs/design/pumbility-overhaul.md §4.15, D69): what a
///     Phoenix 2 top-50 pool is graded at across every account the site holds one for, what the
///     per-player average of it looks like, and how a handful of candidate cut sets split that
///     population — including the shipped one.
///     <para>
///         It reads through the published contracts only: <c>GetTop50ForPlayerQuery</c> for the
///         fifty, so the pool it bands is the one the site itself builds, and
///         <c>GetPumbilityTitleCohortQuery</c> for the cohort the Breakdown card draws. Board-only
///         players are outside it — they have no account to ask — so the board half of §4.15 stays
///         a measurement in the doc rather than something this can regenerate.
///     </para>
///     <para>
///         Configure <c>CatalogProbe:ConnectionString</c> or SCORETRACKER_CATALOG_CONNECTION;
///         SCORETRACKER_PUMBILITY_PROBE_USER picks whose cohort is read. Read-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PumbilityArchetypeProbeTests
{
    /// <summary>
    ///     The candidate cut sets §4.15 priced, shipped one first. Named rather than generated: the
    ///     point of the probe is that the rejected sets stay measurable beside the one that won.
    /// </summary>
    private static readonly (string Name, int[] Cuts)[] CandidateSets =
    {
        ("shipped — grade rungs", new[] { 970_000, 975_000, 980_000, 985_000 }),
        ("the target shape", new[] { 967_500, 976_250, 981_500, 986_500 }),
        ("Phoenix 1's, for reference", new[] { 950_000, 970_000, 980_000, 995_000 })
    };

    private static readonly string[] ArchetypeNames =
        { "Pass Pusher", "Pass Refiner", "Balanced Player", "Competitive", "Perfectionist" };

    private readonly ITestOutputHelper _output;

    public PumbilityArchetypeProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Guid? ProbeUserId =>
        Guid.TryParse(Environment.GetEnvironmentVariable("SCORETRACKER_PUMBILITY_PROBE_USER"), out var fromEnv)
            ? fromEnv
            : null;

    [CatalogProbeFact]
    public async Task How_a_phoenix_2_fifty_is_graded_and_where_the_cuts_fall()
    {
        await using var services = BuildServices();
        var mediator = services.GetRequiredService<IMediator>();
        var stats = services.GetRequiredService<IPlayerStatsRepository>();
        const MixEnum mix = MixEnum.Phoenix2;

        var userIds = (await stats.GetUserIdsWithStats(mix, CancellationToken.None)).ToArray();
        _output.WriteLine($"accounts with {mix} stats: {userIds.Length}");

        // Every account's fifty, through the query the site builds it with. An account under the
        // sample-size guard is counted separately rather than dropped silently — the guard is a
        // real part of who wears a chip.
        var slotGrades = new Dictionary<PhoenixLetterGrade, int>();
        var averages = new List<double>();
        var shortPools = 0;
        foreach (var userId in userIds)
        {
            var fifty = (await mediator.Send(new GetTop50ForPlayerQuery(userId, null, 50, mix),
                    CancellationToken.None))
                .Where(s => s.Score != null)
                .Select(s => s.Score!.Value)
                .ToArray();
            if (fifty.Length < RecapPlayerTypeCalculator.MinimumScores)
            {
                shortPools++;
                continue;
            }

            foreach (var score in fifty)
            {
                var grade = score.LetterGradeFor(mix);
                slotGrades[grade] = slotGrades.GetValueOrDefault(grade) + 1;
            }

            averages.Add(fifty.Average(s => (int)s));
        }

        _output.WriteLine($"banded: {averages.Count} · under the {RecapPlayerTypeCalculator.MinimumScores}-chart " +
                          $"guard: {shortPools}");
        if (averages.Count == 0) return;

        _output.WriteLine("");
        _output.WriteLine("-- every pool slot, by grade --");
        var slots = slotGrades.Values.Sum();
        foreach (var (grade, count) in slotGrades.OrderByDescending(kv => (int)kv.Key.GetMinimumScoreFor(mix)))
            _output.WriteLine($"  {grade.GetName(),-5} {count,7}  {100.0 * count / slots,5:F1}%");

        averages.Sort();
        _output.WriteLine("");
        _output.WriteLine("-- the per-player average --");
        foreach (var q in new[] { 0.05, 0.10, 0.25, 0.50, 0.75, 0.90, 0.95 })
            _output.WriteLine($"  p{q * 100,-3:F0} {Quantile(averages, q),10:N0}");

        _output.WriteLine("");
        _output.WriteLine("-- how many stand at or above each grade floor --");
        foreach (var grade in new[]
                 {
                     PhoenixLetterGrade.SSSPlus, PhoenixLetterGrade.SSS, PhoenixLetterGrade.SSPlus,
                     PhoenixLetterGrade.SS, PhoenixLetterGrade.SPlus, PhoenixLetterGrade.S,
                     PhoenixLetterGrade.AAAPlus, PhoenixLetterGrade.AAA
                 })
        {
            var floor = (int)grade.GetMinimumScoreFor(mix);
            var at = averages.Count(a => a >= floor);
            _output.WriteLine($"  {grade.GetName(),-5} {floor,9:N0}  {at,5}  {100.0 * at / averages.Count,5:F1}%");
        }

        _output.WriteLine("");
        _output.WriteLine("-- the candidate cut sets --");
        foreach (var (name, cuts) in CandidateSets)
        {
            var shape = Shape(averages, cuts);
            _output.WriteLine($"  {name,-28} {string.Join(" / ", cuts.Select(c => $"{c:N0}"))}");
            _output.WriteLine($"  {"",-28} {string.Join(" / ", shape.Select(s => $"{s,4:F1}"))}");
        }

        // The shipped set and the calculator must agree, or the section and the chip would cut
        // differently. Once the calculator takes a mix this passes it; until then it bands the
        // mix it defaults to.
        var shipped = Shape(averages, CandidateSets[0].Cuts);
        var throughCalculator = new double[5];
        foreach (var average in averages) throughCalculator[(int)RecapPlayerTypeCalculator.FromAverage(average)]++;
        for (var i = 0; i < 5; i++) throughCalculator[i] = 100.0 * throughCalculator[i] / averages.Count;
        _output.WriteLine("");
        _output.WriteLine($"  through RecapPlayerTypeCalculator: {string.Join(" / ", throughCalculator.Select(s => $"{s,4:F1}"))}");
        _output.WriteLine($"  agrees with the shipped set:       {shipped.Zip(throughCalculator).All(p => Math.Abs(p.First - p.Second) < 0.05)}");

        // And the cohort the card actually draws the spectrum over.
        var viewer = ProbeUserId ?? userIds.First();
        var cohort = await mediator.Send(new GetPumbilityTitleCohortQuery(viewer, mix), CancellationToken.None);
        _output.WriteLine("");
        _output.WriteLine($"-- the cohort for {viewer} --");
        _output.WriteLine($"  band {cohort.Band?.ToString() ?? "(none)"} · {cohort.Holders} holders · " +
                          $"{cohort.BoardHolders} of them board-only · swept {cohort.BoardAsOf?.ToString("u") ?? "never"}");

        Assert.True(true, "a measurement, not a guarantee — read the output");
    }

    private static double Quantile(IReadOnlyList<double> sorted, double q)
    {
        if (sorted.Count == 1) return sorted[0];
        var at = q * (sorted.Count - 1);
        var lower = (int)Math.Floor(at);
        var upper = (int)Math.Ceiling(at);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (at - lower);
    }

    /// <summary>The five shares a cut set splits these averages into, weakest band first.</summary>
    private static double[] Shape(IReadOnlyCollection<double> averages, IReadOnlyList<int> cuts)
    {
        var counts = new double[5];
        foreach (var average in averages)
        {
            var band = 0;
            for (var i = cuts.Count - 1; i >= 0; i--)
                if (average >= cuts[i])
                {
                    band = i + 1;
                    break;
                }

            counts[band]++;
        }

        for (var i = 0; i < counts.Length; i++) counts[i] = 100.0 * counts[i] / averages.Count;
        return counts;
    }

    private static ServiceProvider BuildServices()
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
        services.AddCatalog();
        services.AddScoreLedger();
        services.AddChartIntelligence();
        services.AddPlayerProgress();
        // The cohort reads the official board for the players no account claims (D68); nothing here scrapes.
        services.AddOfficialMirror();
        services.AddTransient<IScoreProjector, ScoreProjector>();
        services.AddSingleton<IDateTimeOffsetAccessor>(new SystemClock());
        services.AddSingleton(Mock.Of<IBus>());
        services.AddSingleton(Mock.Of<ICurrentUserAccessor>());
        return services.BuildServiceProvider();
    }

    private sealed class SystemClock : IDateTimeOffsetAccessor
    {
        public DateTimeOffset Now => DateTimeOffset.UtcNow;
    }
}
