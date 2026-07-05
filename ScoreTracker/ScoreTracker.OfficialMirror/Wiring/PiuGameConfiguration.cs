using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Wiring;

/// <summary>
///     Endpoints for the official PiuGame sites. Defaults are the production hosts;
///     the E2E suite points <see cref="BaseUrl" /> (and friends) at a WireMock stub
///     serving snapshotted pages. Bound from the host's "PiuGame" configuration
///     section — an unconfigured host keeps today's behavior.
/// </summary>
public sealed class PiuGameConfiguration
{
    /// <summary>The Phoenix (1) site. Property name predates the parallel-mix split — kept for config binding compat.</summary>
    public string BaseUrl { get; set; } = "https://phoenix.piugame.com";

    /// <summary>The Phoenix 2 site. Same page structure as Phoenix 1; stepball images gain a /p2/ path segment.</summary>
    public string Phoenix2BaseUrl { get; set; } = "https://piugame.com";

    public string UcsBaseUrl { get; set; } = "https://ucs.piugame.com";

    /// <summary>
    ///     The am-pass SSO host that fronts BOTH piugame sites (verified 2026-07-04) — see the
    ///     SSO-bounce handling in PiuGameApi. Stubbed in tests so login never leaves the machine.
    /// </summary>
    public string AmPassUrl { get; set; } = "https://am-pass.net";

    /// <summary>
    ///     The official-site host for a mix. XX has no scrapeable site; anything unknown
    ///     throws loudly rather than silently scraping the wrong mix's boards.
    /// </summary>
    public string BaseUrlFor(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.Phoenix => BaseUrl,
            MixEnum.Phoenix2 => Phoenix2BaseUrl,
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix, "No official site host for mix")
        };
    }
}
