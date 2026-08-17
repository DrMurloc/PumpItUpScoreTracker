using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.DevTooling;

/// <summary>
///     Reads the live site's public API and hands the result to <see cref="IDevCatalogWriter" />.
///     <para>
///         It reads <c>api/v2/*</c> with a personal token, exactly as any integrator would. That is
///         the point rather than a convenience — if the harness can rebuild a working database from
///         the public surface, the public surface is complete, and if it cannot, we find out here
///         instead of from someone building a tool.
///     </para>
/// </summary>
internal sealed class DevApiReader
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Every list the tier-list endpoint publishes. Two of the three are Phoenix-only and answer
    ///     with a 404 elsewhere, which the harness treats as "not here" rather than as a failure —
    ///     asking and being told beats encoding the rule in a second place.
    /// </summary>
    private static readonly string[] TierLists = { "score-difficulty", "pass-difficulty", "pg-difficulty" };

    /// <summary>
    ///     Every route this reader calls, as templates.
    ///     <para>
    ///         Hoisted out of the call sites so a test can enumerate them and check each resolves
    ///         against the app's registered routes. That is not ceremony: this harness once asked for
    ///         <c>api/v2/chart-analysis/chart-scoring-levels</c>, which had never existed, and because
    ///         the call does not tolerate a miss it broke /Dev/Populate outright — silently, for two
    ///         commits, because nothing ran the sync end to end.
    ///     </para>
    /// </summary>
    internal static IReadOnlyList<string> RouteTemplates { get; } = new[]
    {
        "api/v2/mixes",
        "api/v2/songs",
        "api/v2/charts",
        "api/v2/players/{playerId}/scores"
    }.Concat(TierLists.Select(t => $"api/v2/tier-lists/{t}")).ToArray();

    private readonly IMemoryCache _cache;
    private readonly IDevCatalogWriter _writer;
    private readonly string _baseUrl;

    public DevApiReader(IDevCatalogWriter writer, string baseUrl, IMemoryCache cache)
    {
        _writer = writer;
        _baseUrl = baseUrl;
        _cache = cache;
    }

    public async Task Populate(string apiToken, Guid localUserId, Action<string> reportProgress,
        CancellationToken cancellationToken = default)
    {
        // Its own client rather than one from IHttpClientFactory: ServiceDefaults wraps every
        // client the factory hands out in the standard resilience handler, whose total-request
        // timeout is 30 seconds and sits outside HttpClient.Timeout — so a catalog page that
        // takes longer is rejected no matter what ceiling is set below. This is a one-shot bulk
        // download of someone's whole local database, which wants a long ceiling rather than a
        // per-request policy sized for the app's own outbound calls.
        using var client = new HttpClient();
        client.BaseAddress = new Uri(_baseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromMinutes(5);
        // Basic with a personal API token — the same scheme a partner tool authenticates with.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"dev:{apiToken}")));

        reportProgress("Downloading mixes…");
        var mixes = await Page<MixWire>(client, "api/v2/mixes", reportProgress, cancellationToken);

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
            foreach (var song in await Page<SongWire>(client, $"api/v2/songs?mix={wire.Name}",
                         reportProgress, cancellationToken))
                songs.TryAdd(song.Name, new DevSongRow(song.Name, song.Type, song.Artist,
                    song.DurationSeconds, song.ImageUrl, song.Bpm?.Min, song.Bpm?.Max));

            foreach (var chart in await Page<ChartWire>(client, $"api/v2/charts?mix={wire.Name}",
                         reportProgress, cancellationToken))
            {
                if (!Enum.TryParse<MixEnum>(chart.OriginalMix, out var originalMix)) originalMix = mix;
                charts.Add(new DevChartRow(chart.Id, mix, originalMix, chart.SongName, chart.Type,
                    chart.Level, chart.NoteCount, chart.PlayerCount, chart.StepArtist, chart.LegacySlot));
                // Scoring level rides on the chart now, so this is one fewer pass per mix.
                if (chart.ScoringLevel is not null)
                    scoringLevels.Add(new DevScoringLevelRow(mix, chart.Id, chart.ScoringLevel.Value));
            }

            foreach (var list in TierLists)
            foreach (var entry in await Page<TierListWire>(client,
                         $"api/v2/tier-lists/{list}?mix={wire.Name}", reportProgress,
                         cancellationToken, skipMissing: true))
                tierLists.Add(new DevTierListRow(StoredName(list, mix), mix, entry.ChartId,
                    entry.Category, entry.Order));

        }

        reportProgress($"Writing {charts.Count:N0} charts to the local database…");
        await _writer.ReplaceCatalog(new DevCatalogSnapshot(
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
                         $"api/v2/players/me/scores?mix={wire.Name}", reportProgress, cancellationToken))
                scores.Add(new DevScoreRow(score.ChartId, mix, score.RecordedAt, score.Score,
                    score.LetterGrade, score.Plate, score.IsBroken, score.Source,
                    score.Judgments?.Perfects, score.Judgments?.Greats, score.Judgments?.Goods,
                    score.Judgments?.Bads, score.Judgments?.Misses, score.Judgments?.MaxCombo));
        }

        reportProgress($"Writing {scores.Count:N0} scores…");
        await _writer.ReplaceUserScores(localUserId, scores, cancellationToken);

        // The seeder writes raw SQL underneath the repositories, so every cached read (charts per
        // mix, song names, videos, skills, ...) is stale — including the empty chart list the login
        // page uses to decide you're on an empty database. Everything cached came from pre-import
        // state; clear it all.
        if (_cache is MemoryCache concrete) concrete.Clear();

        reportProgress("Done.");
    }

    /// <summary>
    ///     Asserts at the call site that the path is one of the declared templates, so a URL typed
    ///     directly into a request can never diverge from the list the route test checks.
    /// </summary>
    private static string Route(string path)
    {
        return RouteTemplates.Contains(path)
            ? path
            : throw new InvalidOperationException($"{path} is not in RouteTemplates.");
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
    internal static async Task<IReadOnlyList<T>> Page<T>(HttpClient client, string path,
        Action<string> reportProgress, CancellationToken cancellationToken, bool skipMissing = false)
    {
        var all = new List<T>();
        var next = path;
        while (next is not null)
        {
            var url = next;
            var page = await WithRetries(async () =>
            {
                using var response = await Get(client, url, reportProgress, cancellationToken);
                if (skipMissing && response.StatusCode == HttpStatusCode.NotFound) return null;

                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonSerializer.DeserializeAsync<PageWire<T>>(stream, Wire, cancellationToken);
            }, cancellationToken);

            // Null covers both a skipped 404 and a body that deserialized to nothing; neither has
            // a cursor to follow, so the collection ends here.
            if (page is null) return all;
            if (page.Data is not null) all.AddRange(page.Data);
            next = page.Next;
        }

        return all;
    }

    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(1);

    private const int MaxRateLimitWaits = 3;
    private static readonly TimeSpan DefaultRateLimitWait = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     The window resets on a whole second and <c>Retry-After</c> is written truncated, so
    ///     waiting exactly as long as it says lands just before the reset and earns a second 429.
    /// </summary>
    private static readonly TimeSpan RateLimitMargin = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     A GET that waits the rate limit out instead of failing on it.
    ///     <para>
    ///         api/v2 allows 60 requests a minute against a personal token in a fixed window, and
    ///         rebuilding a database is several hundred requests — so a full sync does not merely
    ///         risk 429, it is guaranteed several. That makes waiting the normal path rather than
    ///         an error path, and the response says exactly how long to wait, which is the whole
    ///         reason the API sends <c>Retry-After</c>.
    ///     </para>
    ///     <para>
    ///         The wait is announced. A minute of silence on a page whose only other signal is a
    ///         spinner reads as a hang, and someone who believes it hung presses Populate again,
    ///         which is precisely the thing the limit is there to stop.
    ///     </para>
    /// </summary>
    private static async Task<HttpResponseMessage> Get(HttpClient client, string url,
        Action<string> reportProgress, CancellationToken cancellationToken)
    {
        for (var waited = 0;; waited++)
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || waited >= MaxRateLimitWaits)
                return response;

            // Only the delta form: this API writes whole seconds, and reading the HTTP-date form
            // would need a clock this class has no business holding.
            var wait = (response.Headers.RetryAfter?.Delta ?? DefaultRateLimitWait) + RateLimitMargin;
            response.Dispose();

            reportProgress($"Rate limited by the API — waiting {wait.TotalSeconds:N0}s…");
            await Task.Delay(wait, cancellationToken);
        }
    }

    /// <summary>
    ///     One attempt is not a verdict on a home connection. This pulls hundreds of pages and has
    ///     no way to resume, so a single reset two thirds of the way through would otherwise cost
    ///     the whole sync and start it over from nothing. Backoff grows between tries.
    ///     <para>
    ///         A cancelled run stops at once — but only when it is the caller's token that fired.
    ///         HttpClient reports its own request timeout as a cancellation too, and that one is
    ///         precisely the transient worth surviving.
    ///     </para>
    /// </summary>
    private static async Task<T> WithRetries<T>(Func<Task<T>> attempt, CancellationToken cancellationToken)
    {
        for (var retry = 0;; retry++)
            try
            {
                return await attempt();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (retry < MaxRetries && IsTransient(e))
            {
                await Task.Delay(RetryBaseDelay * (1 << retry), cancellationToken);
            }
    }

    /// <summary>
    ///     Worth asking again: a connection that never landed, HttpClient's own timeout, or a
    ///     server that fell over.
    ///     <para>
    ///         A status the server chose below 500 is its answer rather than a blip, and it will
    ///         give the same one seven seconds later — a wrong token (401) and a route that does
    ///         not exist (404) both fail on the first attempt, so the page can say so while the
    ///         person who pressed Populate is still watching it. A 429 never reaches here: it is
    ///         waited out against <c>Retry-After</c> in <see cref="Get" />, and a 429 that arrives
    ///         even after that is a real refusal rather than a queue to sit in.
    ///     </para>
    /// </summary>
    private static bool IsTransient(Exception e)
    {
        return e switch
        {
            HttpRequestException { StatusCode: { } status } =>
                status >= HttpStatusCode.InternalServerError
                || status is HttpStatusCode.RequestTimeout,
            // No status at all: DNS, a refused connection, a reset mid-handshake.
            HttpRequestException => true,
            // Dropped mid-body, while the response stream was being read.
            IOException => true,
            // HttpClient.Timeout. The caller's own cancellation is caught before this runs.
            OperationCanceledException => true,
            _ => false
        };
    }

    // The wire shapes, declared locally rather than shared with the controllers' DTOs: the harness
    // is a consumer of the published API and should break the same way an integrator would.
    private sealed record PageWire<T>(T[]? Data, string? Next);

    private sealed record MixWire(string Name, string DisplayName, int SortOrder, bool IsPrimary);

    private sealed record BpmWire(decimal? Min, decimal? Max);

    private sealed record SongWire(string Name, string Type, string Artist, int DurationSeconds,
        string ImageUrl, BpmWire? Bpm);

    private sealed record ChartWire(Guid Id, string Mix, string OriginalMix, string SongName, string Type,
        int Level, int? NoteCount, int PlayerCount, string? StepArtist, string? LegacySlot,
        double? ScoringLevel);

    private sealed record TierListWire(Guid ChartId, string Category, int Order);


    private sealed record JudgmentsWire(int? Perfects, int? Greats, int? Goods, int? Bads, int? Misses,
        int? MaxCombo = null);

    private sealed record ScoreWire(Guid ChartId, DateTimeOffset RecordedAt, string? Source, int? Score,
        string? LetterGrade, string? Plate, bool IsBroken, JudgmentsWire? Judgments);
}
