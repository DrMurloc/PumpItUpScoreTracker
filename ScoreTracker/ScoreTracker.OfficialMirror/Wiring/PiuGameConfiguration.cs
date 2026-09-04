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
    ///     Service-account credentials for the login-gated Phoenix 2 leaderboards — unlike
    ///     Phoenix, piugame.com serves NO anonymous ranking traffic (pumbility_ranking.php
    ///     and the over_ranking chart list both require a session; verified 2026-07-09).
    ///     Unset = Phoenix 2 leaderboard imports throw with a pointer here. Use a dedicated
    ///     dummy account, never a real player's.
    /// </summary>
    public string? ServiceUsername { get; set; }

    public string? ServicePassword { get; set; }

    /// <summary>
    ///     Pause between consecutive requests inside the weekly board sweep — hundreds of
    ///     board fetches against a login-gated site deserve pacing. Zero disables (tests).
    /// </summary>
    public int SweepRequestDelayMilliseconds { get; set; } = 250;

    /// <summary>
    ///     First pause before a failed request is retried; each further retry doubles it
    ///     (1s, 2s, 4s by default). The official site's edge resets connections mid-handshake
    ///     under load, and coming straight back a second later tends to be refused the same
    ///     way. Zero disables the waiting (tests).
    /// </summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    ///     The official-site host for a mix. Only the two Phoenix sites are mapped, because
    ///     only they serve the pages this ACL scrapes — per-chart boards and personal bests.
    ///     <para>
    ///         XX is <b>not</b> unmapped for lack of a site: xx.piugame.com is alive and has its
    ///         own total/single/double ranking boards plus an avatar shop (the manual-avatar
    ///         catalog is seeded from it, see docs/design/avatar-selection.md). It has no
    ///         per-chart leaderboard or my-best-score page, which is what this host lookup feeds,
    ///         so pointing an importer at it would find nothing. Anything unknown throws loudly
    ///         rather than silently scraping the wrong mix's boards.
    ///     </para>
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
