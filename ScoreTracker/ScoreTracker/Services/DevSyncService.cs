using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Configuration;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Dev harness importer: builds a local database out of the public API.
///     <para>
///         It reads <c>api/v2/*</c> with a personal token, exactly as any integrator would. That is
///         the point rather than a convenience — if the harness can rebuild a working database from
///         the public surface, the public surface is complete, and if it cannot, we find out here
///         instead of from someone building a tool.
///     </para>
/// </summary>
public sealed class DevSyncService
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Every list the tier-list endpoint publishes. Two of the three are Phoenix-only and answer
    ///     with a 404 elsewhere, which the harness treats as "not here" rather than as a failure —
    ///     asking and being told beats encoding the rule in a second place.
    /// </summary>
    private static readonly string[] TierLists = { "score-difficulty", "pass-difficulty", "pg-difficulty" };

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ProdSyncConfiguration> _options;
    private readonly IDevCatalogSeeder _seeder;

    public DevSyncService(IHttpClientFactory httpClientFactory, IDevCatalogSeeder seeder,
        IOptions<ProdSyncConfiguration> options, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _seeder = seeder;
        _options = options;
        _cache = cache;
    }

    public async Task Sync(string apiToken, Guid localUserId, Action<string> reportProgress,
        CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromMinutes(5);
        // Basic with a personal API token — the same scheme a partner tool authenticates with.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"dev:{apiToken}")));

        reportProgress("Downloading mixes…");
        var mixes = await Page<MixWire>(client, "api/v2/mixes", cancellationToken);

        var songs = new Dictionary<string, DevSongRow>(StringComparer.Ordinal);
        var charts = new List<DevChartRow>();
        var tierLists = new List<DevTierListRow>();
        var scoringLevels = new List<DevScoringLevelRow>();

        foreach (var wire in mixes)
        {
            if (!Enum.TryParse<MixEnum>(wire.Name, out var mix)) continue;

            reportProgress($"Downloading {wire.DisplayName}…");

            // Songs repeat across mixes; first one wins, and they are identical by construction
            // (the catalog stores one song row and every mix's charts point at it).
            foreach (var song in await Page<SongWire>(client, $"api/v2/songs?mix={wire.Name}", cancellationToken))
                songs.TryAdd(song.Name, new DevSongRow(song.Name, song.Type, song.Artist,
                    song.DurationSeconds, song.ImageUrl, song.Bpm?.Min, song.Bpm?.Max));

            foreach (var chart in await Page<ChartWire>(client, $"api/v2/charts?mix={wire.Name}", cancellationToken))
            {
                if (!Enum.TryParse<MixEnum>(chart.OriginalMix, out var originalMix)) originalMix = mix;
                charts.Add(new DevChartRow(chart.Id, mix, originalMix, chart.SongName, chart.Type,
                    chart.Level, chart.NoteCount, chart.PlayerCount, chart.StepArtist, chart.LegacySlot));
            }

            foreach (var list in TierLists)
            foreach (var entry in await Page<TierListWire>(client,
                         $"api/v2/tier-lists/{list}?mix={wire.Name}", cancellationToken,
                         skipMissing: true))
                tierLists.Add(new DevTierListRow(StoredName(list, mix), mix, entry.ChartId,
                    entry.Category, entry.Order));

            foreach (var level in await Page<ScoringLevelWire>(client,
                         $"api/v2/chart-scoring-levels?mix={wire.Name}", cancellationToken))
                scoringLevels.Add(new DevScoringLevelRow(mix, level.ChartId, level.ScoringLevel));
        }

        reportProgress($"Writing {charts.Count:N0} charts to the local database…");
        await _seeder.ReplaceCatalog(new DevCatalogSnapshot(
            mixes.Where(m => Enum.TryParse<MixEnum>(m.Name, out _))
                .Select(m => new DevMixRow(Enum.Parse<MixEnum>(m.Name), m.DisplayName, m.SortOrder, m.IsPrimary))
                .ToArray(),
            songs.Values.ToArray(), charts, tierLists, scoringLevels), cancellationToken);

        // Scores are per mix and only the primary ones have any worth pulling — a legacy mix's
        // records are a handful of hand-entered rows, and asking for all 29 triples the round trips.
        var scores = new List<DevScoreRow>();
        foreach (var wire in mixes.Where(m => m.IsPrimary))
        {
            if (!Enum.TryParse<MixEnum>(wire.Name, out var mix)) continue;

            reportProgress($"Downloading your {wire.DisplayName} scores…");
            foreach (var score in await Page<ScoreWire>(client,
                         $"api/v2/players/me/scores?mix={wire.Name}", cancellationToken))
                scores.Add(new DevScoreRow(score.ChartId, mix, score.RecordedAt, score.Score,
                    score.LetterGrade, score.Plate, score.IsBroken, score.Source,
                    score.Judgments?.Perfects, score.Judgments?.Greats, score.Judgments?.Goods,
                    score.Judgments?.Bads, score.Judgments?.Misses));
        }

        reportProgress($"Writing {scores.Count:N0} scores…");
        await _seeder.ReplaceUserScores(localUserId, scores, cancellationToken);

        // The seeder writes raw SQL underneath the repositories, so every cached read (charts per
        // mix, song names, videos, skills, ...) is stale — including the empty chart list the login
        // page uses to decide you're on an empty database. Everything cached came from pre-import
        // state; clear it all.
        if (_cache is MemoryCache concrete) concrete.Clear();

        reportProgress("Done.");
    }

    /// <summary>
    ///     Route value back to the name the list is stored under. Mirrors the controller's map,
    ///     including that pass difficulty is one row before Phoenix and another from Phoenix on.
    /// </summary>
    private static string StoredName(string route, MixEnum mix)
    {
        return route switch
        {
            "score-difficulty" => "Scores",
            "pg-difficulty" => "PG",
            _ => mix.UsesLegacyScoring() ? "Difficulty" : "Pass Count"
        };
    }

    /// <summary>
    ///     Follows <c>next</c> to the end. The cursor is opaque and belongs to its filters, so the
    ///     URL is used as given rather than rebuilt.
    /// </summary>
    /// <param name="skipMissing">
    ///     Treat a 404 as an empty result. Only for collections the API publishes per mix — a
    ///     Phoenix-only list asked for on a legacy mix is a correct "not here", not a failed sync.
    /// </param>
    private static async Task<IReadOnlyList<T>> Page<T>(HttpClient client, string path,
        CancellationToken cancellationToken, bool skipMissing = false)
    {
        var all = new List<T>();
        var next = path;
        while (next is not null)
        {
            using var response = await client.GetAsync(next, cancellationToken);
            if (skipMissing && response.StatusCode == HttpStatusCode.NotFound) return all;

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var page = await JsonSerializer.DeserializeAsync<PageWire<T>>(stream, Wire, cancellationToken);
            if (page?.Data is not null) all.AddRange(page.Data);
            next = page?.Next;
        }

        return all;
    }

    // The wire shapes, declared locally rather than shared with the controllers' DTOs: the harness
    // is a consumer of the published API and should break the same way an integrator would.
    private sealed record PageWire<T>(T[]? Data, string? Next);

    private sealed record MixWire(string Name, string DisplayName, int SortOrder, bool IsPrimary);

    private sealed record BpmWire(decimal? Min, decimal? Max);

    private sealed record SongWire(string Name, string Type, string Artist, int DurationSeconds,
        string ImageUrl, BpmWire? Bpm);

    private sealed record ChartWire(Guid Id, string Mix, string OriginalMix, string SongName, string Type,
        int Level, int? NoteCount, int PlayerCount, string? StepArtist, string? LegacySlot);

    private sealed record TierListWire(Guid ChartId, string Category, int Order);

    private sealed record ScoringLevelWire(Guid ChartId, double ScoringLevel);

    private sealed record JudgmentsWire(int? Perfects, int? Greats, int? Goods, int? Bads, int? Misses);

    private sealed record ScoreWire(Guid ChartId, DateTimeOffset RecordedAt, string? Source, int? Score,
        string? LetterGrade, string? Plate, bool IsBroken, JudgmentsWire? Judgments);
}
