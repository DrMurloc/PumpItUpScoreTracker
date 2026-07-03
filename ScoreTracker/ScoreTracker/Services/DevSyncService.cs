using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Configuration;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Dev harness importer: pulls the allowlisted raw table exports from the configured
///     site's /dev/export endpoints (authenticated exactly like a partner API caller) and
///     replays them into the local database. Reference tables are replaced wholesale; the
///     remote account's scores are rewritten onto the given local user.
/// </summary>
public sealed class DevSyncService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDevDataTransfer _transfer;
    private readonly IOptions<ProdSyncConfiguration> _options;
    private readonly IMemoryCache _cache;

    public DevSyncService(IHttpClientFactory httpClientFactory, IDevDataTransfer transfer,
        IOptions<ProdSyncConfiguration> options, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _transfer = transfer;
        _options = options;
        _cache = cache;
    }

    public async Task Sync(string apiToken, Guid localUserId, Action<string> reportProgress,
        CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"dev:{apiToken}")));

        var rowsByTable = new Dictionary<string, IReadOnlyList<Dictionary<string, JsonElement>>>();
        foreach (var tableKey in _transfer.ReferenceTableKeys)
        {
            reportProgress($"Downloading {tableKey}…");
            rowsByTable[tableKey] = await Fetch(client, $"dev/export/{tableKey}", cancellationToken);
        }

        reportProgress("Writing reference data to the local database…");
        await _transfer.ReplaceReferenceTables(rowsByTable, cancellationToken);

        reportProgress("Downloading your scores…");
        var scores = await Fetch(client, "dev/export/myscores", cancellationToken);

        reportProgress($"Writing {scores.Count:N0} scores…");
        await _transfer.ReplaceUserScores(localUserId, scores, cancellationToken);

        // The import writes raw SQL underneath the repositories, so every cached read
        // (charts per mix, song names, videos, skills, ...) is stale — including the
        // empty chart list the login page uses to decide you're on an empty database.
        // Everything cached came from pre-import state; clear it all.
        if (_cache is MemoryCache concrete) concrete.Clear();

        reportProgress("Done.");
    }

    private static async Task<IReadOnlyList<Dictionary<string, JsonElement>>> Fetch(HttpClient client, string path,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<List<Dictionary<string, JsonElement>>>(stream,
                   cancellationToken: cancellationToken)
               ?? new List<Dictionary<string, JsonElement>>();
    }
}
