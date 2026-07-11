using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.Apis
{
    /// <summary>
    ///     Client over piucenter.com's published data. The site is a static-hosted SPA:
    ///     all data lives as JSON under /chart-jsons/&lt;version&gt;/, the version is
    ///     hardcoded in the app's data-*.js bundle, and unknown files come back as the
    ///     HTML app shell with HTTP 200 — so existence is decided by content sniffing,
    ///     never status codes.
    /// </summary>
    public sealed class PiuCenterApi : IPiuCenterClient
    {
        private const string ChartTableFile = "page-content/chart-table.json";
        private const string SkillsFile = "page-content/stepchart-skills.json";
        private const string TierListsFile = "page-content/tierlists.json";

        private static readonly Regex DataBundlePattern = new(@"/_build/assets/data-[A-Za-z0-9_-]+\.js",
            RegexOptions.Compiled);

        private static readonly Regex VersionPattern = new(@"chart-jsons/(\d+)/", RegexOptions.Compiled);

        private static readonly Regex KeyVariantPattern =
            new("_((?:HALFDOUBLE_)?(?:ARCADE|REMIX|SHORTCUT|FULLSONG))$", RegexOptions.Compiled);

        // Data's shared-client convention (see SkiaShareCardRenderer) — one pooled
        // HttpClient for the process; the injectable overload exists for the approval
        // tests' stubbed handler and is also what DI picks once AddHttpClient is present.
        private static readonly HttpClient SharedHttp = new();

        private readonly HttpClient _http;
        private readonly ILogger<PiuCenterApi> _logger;
        private readonly IOptions<PiuCenterConfiguration> _options;
        private string? _version;

        public PiuCenterApi(IOptions<PiuCenterConfiguration> options, ILogger<PiuCenterApi> logger)
            : this(SharedHttp, options, logger)
        {
        }

        public PiuCenterApi(HttpClient httpClient, IOptions<PiuCenterConfiguration> options,
            ILogger<PiuCenterApi> logger)
        {
            _http = httpClient;
            _options = options;
            _logger = logger;
        }

        public async Task<IReadOnlyList<PiuCenterChartListing>> GetChartTable(
            CancellationToken cancellationToken = default)
        {
            using var document = await GetPageContentFile(ChartTableFile, cancellationToken);
            var listings = new List<PiuCenterChartListing>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var key = row.GetProperty("name").GetString() ?? string.Empty;
                var type = row.GetProperty("sord").GetString() switch
                {
                    "singles" => ChartType.Single,
                    "doubles" => ChartType.Double,
                    _ => (ChartType?)null
                };
                if (key.Length == 0 || type == null) continue;

                var variantMatch = KeyVariantPattern.Match(key);
                listings.Add(new PiuCenterChartListing(
                    key,
                    type.Value,
                    row.GetProperty("level").GetInt32(),
                    row.TryGetProperty("pack", out var pack) ? pack.GetString() ?? string.Empty : string.Empty,
                    variantMatch.Success ? variantMatch.Groups[1].Value : string.Empty,
                    ReadStringArray(row, "skills"),
                    ReadDecimal(row, "NPS") ?? 0,
                    row.TryGetProperty("BPM info", out var bpm) ? bpm.GetString() ?? string.Empty : string.Empty,
                    ReadDecimal(row, "Sustain time") ?? 0,
                    ReadDecimal(row, "Total time under tension") ?? 0));
            }

            return listings;
        }

        public async Task<PiuCenterChartPage?> GetChartPage(string externalKey,
            CancellationToken cancellationToken = default)
        {
            // A key the current data release doesn't know comes back as the SPA shell;
            // GetDataFile turns that into null. The crawl only fetches keys listed in the
            // chart table, so shell-instead-of-JSON here genuinely means "not analyzed".
            using var document = await GetDataFile($"{Uri.EscapeDataString(externalKey)}.json", cancellationToken);
            if (document == null) return null;

            // Root is [notes, holds, metadata]; the metadata object is the last element.
            JsonElement meta = default;
            var found = false;
            foreach (var element in document.RootElement.EnumerateArray())
                if (element.ValueKind == JsonValueKind.Object)
                {
                    meta = element;
                    found = true;
                }

            if (!found)
            {
                _logger.LogWarning("piucenter chart page {Key} had no metadata object", externalKey);
                return null;
            }

            var segmentCount = 0;
            var badgeCounts = new Dictionary<string, int>();
            var rareCounts = new Dictionary<string, int>();
            if (meta.TryGetProperty("Segment metadata", out var segments) &&
                segments.ValueKind == JsonValueKind.Array)
                foreach (var segment in segments.EnumerateArray())
                {
                    segmentCount++;
                    foreach (var badge in ReadStringArray(segment, "Skill badges"))
                        badgeCounts[badge] = badgeCounts.TryGetValue(badge, out var count) ? count + 1 : 1;
                    foreach (var rare in ReadStringArray(segment, "rare skills"))
                        rareCounts[rare] = rareCounts.TryGetValue(rare, out var count) ? count + 1 : 1;
                }

            return new PiuCenterChartPage(
                externalKey,
                ReadStringArray(meta, "chart_skill_summary"),
                segmentCount,
                badgeCounts,
                rareCounts,
                ReadDecimal(meta, "nps_summary"),
                meta.TryGetProperty("notetype_bpm_summary", out var notetype) ? notetype.GetString() : null,
                meta.TryGetProperty("sord_chartlevel", out var sord) ? sord.GetString() : null);
        }

        public async Task<IReadOnlyList<PiuCenterPracticeEntry>> GetPracticeLists(
            CancellationToken cancellationToken = default)
        {
            // Root is [lists, descriptions]; element 0 maps skill -> sord-level -> ranked keys.
            using var document = await GetPageContentFile(SkillsFile, cancellationToken);
            var entries = new List<PiuCenterPracticeEntry>();
            var lists = document.RootElement[0];
            foreach (var skill in lists.EnumerateObject())
            foreach (var level in skill.Value.EnumerateObject())
            {
                var rank = 0;
                foreach (var key in level.Value.EnumerateArray())
                {
                    rank++;
                    var externalKey = key.GetString();
                    if (externalKey != null)
                        entries.Add(new PiuCenterPracticeEntry(skill.Name, level.Name, rank, externalKey));
                }
            }

            return entries;
        }

        public async Task<IReadOnlyDictionary<string, decimal>> GetDifficultyPredictions(
            CancellationToken cancellationToken = default)
        {
            // Folder -> NPS-cluster label -> [keys[], predictions[]]; flattened, a key
            // only appears in its own folder.
            using var document = await GetPageContentFile(TierListsFile, cancellationToken);
            var predictions = new Dictionary<string, decimal>();
            foreach (var folder in document.RootElement.EnumerateObject())
            foreach (var cluster in folder.Value.EnumerateObject())
            {
                if (cluster.Value.ValueKind != JsonValueKind.Array || cluster.Value.GetArrayLength() < 2) continue;
                var keys = cluster.Value[0];
                var values = cluster.Value[1];
                var count = Math.Min(keys.GetArrayLength(), values.GetArrayLength());
                for (var i = 0; i < count; i++)
                {
                    var key = keys[i].GetString();
                    if (key != null && values[i].ValueKind == JsonValueKind.Number)
                        predictions[key] = values[i].GetDecimal();
                }
            }

            return predictions;
        }

        /// <summary>
        ///     page-content files always exist in a valid data release, so a shell response
        ///     means our cached version went stale — re-resolve once and retry.
        /// </summary>
        private async Task<JsonDocument> GetPageContentFile(string relativePath, CancellationToken cancellationToken)
        {
            var document = await GetDataFile(relativePath, cancellationToken);
            if (document != null) return document;

            _version = null;
            document = await GetDataFile(relativePath, cancellationToken);
            return document ??
                   throw new InvalidOperationException(
                       $"piucenter returned the app shell for {relativePath} even after re-resolving the data version");
        }

        private async Task<JsonDocument?> GetDataFile(string relativePath, CancellationToken cancellationToken)
        {
            _version ??= await ResolveVersion(_http, cancellationToken);
            var body = await _http.GetStringAsync(
                $"{BaseUrl}/chart-jsons/{_version}/{relativePath}", cancellationToken);
            var trimmed = body.TrimStart();
            if (trimmed.StartsWith('[') || trimmed.StartsWith('{')) return JsonDocument.Parse(trimmed);
            return null;
        }

        private async Task<string> ResolveVersion(HttpClient client, CancellationToken cancellationToken)
        {
            var shell = await client.GetStringAsync($"{BaseUrl}/", cancellationToken);
            var bundleMatch = DataBundlePattern.Match(shell);
            if (!bundleMatch.Success)
                throw new InvalidOperationException(
                    "piucenter app shell no longer references a data-*.js bundle — the site layout changed");

            var bundle = await client.GetStringAsync($"{BaseUrl}{bundleMatch.Value}", cancellationToken);
            var versionMatch = VersionPattern.Match(bundle);
            if (!versionMatch.Success)
                throw new InvalidOperationException(
                    "piucenter data bundle no longer carries a chart-jsons version — the site layout changed");

            var version = versionMatch.Groups[1].Value;
            _logger.LogInformation("Resolved piucenter data version {Version}", version);
            return version;
        }

        private string BaseUrl => _options.Value.BaseUrl.TrimEnd('/');

        private static IReadOnlyList<string> ReadStringArray(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();
            return array.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToArray();
        }

        private static decimal? ReadDecimal(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDecimal()
                : null;
        }
    }
}
