using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Catalog;

/// <summary>
///     The channel as the site reads it, against a real populated catalog: the per-mix chart
///     dictionary the chart page, the dialog, the facets and the API all read from
///     (docs/design/song-channels.md §3). Runs the real Catalog repository through the vertical's
///     own wiring, so what it prints is what a running app would hold after a fresh start.
///     Read-only. Skips unless configured, like every catalog probe.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SongChannelProbeTests
{
    private readonly ITestOutputHelper _output;

    public SongChannelProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task Phoenix2_charts_carry_their_songs_channel()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        foreach (var contribution in VerticalModelContributions.All())
            services.AddSingleton<IDbModelContribution>(contribution);
        services.AddDbContextFactory<ChartAttemptDbContext>(options =>
            options.UseSqlServer(CatalogProbeConfiguration.ConnectionString));
        services.AddCatalog();
        await using var provider = services.BuildServiceProvider();

        var repository = provider.GetRequiredService<IChartRepository>();
        var charts = (await repository.GetCharts(MixEnum.Phoenix2)).ToArray();
        var byChannel = charts.GroupBy(c => c.Song.Channel).OrderBy(g => g.Key)
            .Select(g => $"{g.Key?.ToString() ?? "(none)"}={g.Count()}");
        _output.WriteLine($"Phoenix 2: {charts.Length} charts — " + string.Join(", ", byChannel));

        var nostalgia = charts.Single(c => c.Song.Name.ToString() == "Nostalgia" && c.Type == ChartType.Double && c.Level == 21);
        _output.WriteLine($"Nostalgia D21: channel {nostalgia.Song.Channel?.ToString() ?? "(none)"}");

        Assert.True(charts.Count(c => c.Song.Channel != null) > 0, "no Phoenix 2 chart carries a channel");
        Assert.Equal(Channel.KPop, nostalgia.Song.Channel);
    }
}
