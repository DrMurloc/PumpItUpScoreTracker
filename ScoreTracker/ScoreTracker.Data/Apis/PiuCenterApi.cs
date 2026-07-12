using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Apis
{
    /// <summary>
    ///     Client over piucenter.com's published data. The site is a static-hosted SPA:
    ///     all data lives as JSON under /chart-jsons/&lt;version&gt;/, the version is
    ///     hardcoded in the app's data-*.js bundle, and unknown files come back as the
    ///     HTML app shell with HTTP 200 — so existence is decided by content sniffing,
    ///     never status codes. Parsing lives in <see cref="PiuCenterDataParser" />,
    ///     shared with the admin snapshot import.
    /// </summary>
    public sealed class PiuCenterApi : IPiuCenterClient
    {
        private const string ChartTableFile = "page-content/chart-table.json";
        private const string SkillsFile = "page-content/stepchart-skills.json";
        private const string TierListsFile = "page-content/tierlists.json";

        private static readonly Regex DataBundlePattern = new(@"/_build/assets/data-[A-Za-z0-9_-]+\.js",
            RegexOptions.Compiled);

        private static readonly Regex VersionPattern = new(@"chart-jsons/(\d+)/", RegexOptions.Compiled);

        // Data's shared-client convention (see SkiaShareCardRenderer) — one pooled
        // HttpClient for the process; the injectable overload exists for the approval
        // tests' stubbed handler and is also what DI picks once AddHttpClient is present.
        private static readonly HttpClient SharedHttp = new();

        private readonly HttpClient _http;
        private readonly ILogger<PiuCenterApi> _logger;
        private readonly IOptions<PiuCenterConfiguration> _options;
        private readonly System.Diagnostics.Stopwatch _throttle = new();
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

        public async Task<string> GetDataVersion(CancellationToken cancellationToken = default)
        {
            return _version ??= await ResolveVersion(_http, cancellationToken);
        }

        public async Task<IReadOnlyList<PiuCenterChartListing>> GetChartTable(
            CancellationToken cancellationToken = default)
        {
            return PiuCenterDataParser.ParseChartTable(await GetPageContentBody(ChartTableFile, cancellationToken));
        }

        public async Task<PiuCenterChartPage?> GetChartPage(string externalKey,
            CancellationToken cancellationToken = default)
        {
            // Politeness throttle between successive per-chart fetches (~1 req/s by
            // default) — the crawl loop calls this thousands of times on a fresh release.
            var remaining = _options.Value.RequestDelayMs - (int)_throttle.ElapsedMilliseconds;
            if (_throttle.IsRunning && remaining > 0) await Task.Delay(remaining, cancellationToken);
            _throttle.Restart();

            // A key the current data release doesn't know comes back as the SPA shell;
            // the parser turns that into null. The crawl only fetches keys listed in the
            // chart table, so shell-instead-of-JSON here genuinely means "not analyzed".
            var body = await GetDataFileBody($"{Uri.EscapeDataString(externalKey)}.json", cancellationToken);
            var page = PiuCenterDataParser.ParseChartPage(externalKey, body);
            if (page == null && PiuCenterDataParser.LooksLikeJson(body))
                _logger.LogWarning("piucenter chart page {Key} had no metadata object", externalKey);
            return page;
        }

        public async Task<IReadOnlyList<PiuCenterPracticeEntry>> GetPracticeLists(
            CancellationToken cancellationToken = default)
        {
            return PiuCenterDataParser.ParsePracticeLists(await GetPageContentBody(SkillsFile, cancellationToken));
        }

        public async Task<IReadOnlyDictionary<string, decimal>> GetDifficultyPredictions(
            CancellationToken cancellationToken = default)
        {
            return PiuCenterDataParser.ParseDifficultyPredictions(
                await GetPageContentBody(TierListsFile, cancellationToken));
        }

        /// <summary>
        ///     page-content files always exist in a valid data release, so a shell response
        ///     means our cached version went stale — re-resolve once and retry.
        /// </summary>
        private async Task<string> GetPageContentBody(string relativePath, CancellationToken cancellationToken)
        {
            var body = await GetDataFileBody(relativePath, cancellationToken);
            if (PiuCenterDataParser.LooksLikeJson(body)) return body;

            _version = null;
            body = await GetDataFileBody(relativePath, cancellationToken);
            return PiuCenterDataParser.LooksLikeJson(body)
                ? body
                : throw new InvalidOperationException(
                    $"piucenter returned the app shell for {relativePath} even after re-resolving the data version");
        }

        private async Task<string> GetDataFileBody(string relativePath, CancellationToken cancellationToken)
        {
            _version ??= await ResolveVersion(_http, cancellationToken);
            return await _http.GetStringAsync(
                $"{BaseUrl}/chart-jsons/{_version}/{relativePath}", cancellationToken);
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
    }
}
