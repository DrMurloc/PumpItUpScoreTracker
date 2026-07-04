namespace ScoreTracker.OfficialMirror.Wiring;

/// <summary>
///     Endpoints for the official PiuGame site. Defaults are the production hosts;
///     the E2E suite points <see cref="BaseUrl" /> (and friends) at a WireMock stub
///     serving snapshotted pages. Bound from the host's "PiuGame" configuration
///     section — an unconfigured host keeps today's behavior.
/// </summary>
public sealed class PiuGameConfiguration
{
    public string BaseUrl { get; set; } = "https://phoenix.piugame.com";
    public string UcsBaseUrl { get; set; } = "https://ucs.piugame.com";

    /// <summary>
    ///     The am-pass SSO host that fronts phoenix.piugame.com — see the SSO-bounce
    ///     handling in PiuGameApi. Stubbed in tests so login never leaves the machine.
    /// </summary>
    public string AmPassUrl { get; set; } = "https://am-pass.net";
}
