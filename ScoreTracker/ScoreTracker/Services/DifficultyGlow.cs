using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Services;

/// <summary>Which end of its folder's weekly count a chart sits at, if either.</summary>
public enum DifficultyGlowKind
{
    None,

    /// <summary>The week's Hardmode list: held by almost nobody. The red glow.</summary>
    Hard,

    /// <summary>The folder's most-held charts. The mint glow.</summary>
    Easy
}

/// <summary>
///     The difficulty glow (docs/design/hardmode-leaderboard.md D32): whether a chart is at the
///     Hardmode end of its folder, at the most-held end, or at neither — and whether this viewer
///     wants either marked. It used to be Hardmode's own mark (D24); it is its own setting now and
///     does not care whether the viewer switched Hardmode on.
///     <para>
///         Circuit-scoped and memoized per mix, exactly like <see cref="ChartScoringLevels" />: a
///         page of forty bubbles must not ask forty times. Both lists come through the Domain port,
///         whose reader caches them and whose census writes them in one save, so a bubble never
///         pairs this week's red with last week's green.
///     </para>
/// </summary>
public sealed class DifficultyGlow
{
    /// <summary>
    ///     Off means "do not glow"; the setting's ABSENCE means on, because the glow defaults on for
    ///     everyone including signed-out visitors (D24). Storing only the opt-out keeps the default
    ///     free. The stored key predates D32 and is kept on purpose: a player who turned off "Mark
    ///     Hardmode charts" keeps the glow off without a migration to say so.
    /// </summary>
    public const string SettingKey = "Universal__HideHardmodeMark";

    private readonly Dictionary<MixEnum, Ends> _byMix = new();
    private readonly ICurrentUserAccessor _currentUser;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IHardmodeChartReader _reader;
    private readonly IUiSettingsAccessor _settings;
    private bool? _enabled;

    public DifficultyGlow(IHardmodeChartReader reader, IUiSettingsAccessor settings,
        ICurrentUserAccessor currentUser)
    {
        _reader = reader;
        _settings = settings;
        _currentUser = currentUser;
    }

    /// <summary>
    ///     Whether this viewer shows the glow at all. Defaults on, and a signed-out visitor has no
    ///     settings to read — they get the default like anyone else.
    ///     <para>
    ///         ⚠ The signed-out branch must SHORT-CIRCUIT rather than ask the settings accessor. Its
    ///         anonymous path is ProtectedBrowserStorage, which is JS interop and throws outside a live
    ///         circuit — and the difficulty bubble renders on statically-rendered pages (the chart page,
    ///         the weekly charts page), so asking there 500s the page for every signed-out visitor.
    ///         <c>ScoreColorPreferences</c> guards the same way for the same reason.
    ///     </para>
    /// </summary>
    public async Task<bool> IsEnabled()
    {
        if (!_currentUser.IsLoggedIn) return true;
        // The result, not the task - same reason as the lists below.
        return _enabled ??= await _settings.GetSetting(SettingKey) is null;
    }

    /// <summary>
    ///     The glow this chart wears for this viewer. One call answers the bubble's whole question, so
    ///     a caller cannot get the setting and the lists out of step. None on any mix without a census,
    ///     which is every mix but Phoenix 2 today. The census keeps the two ends apart; Hard wins if a
    ///     chart were ever in both, because a Hardmode chart that also glowed "most held" would lie.
    /// </summary>
    public async Task<DifficultyGlowKind> GlowFor(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsEnabled()) return DifficultyGlowKind.None;
        var ends = await EndsFor(mix, cancellationToken);
        return ends.Hard.Contains(chartId) ? DifficultyGlowKind.Hard
            : ends.Easy.Contains(chartId) ? DifficultyGlowKind.Easy
            : DifficultyGlowKind.None;
    }

    /// <summary>
    ///     The mix's two ends, read once per circuit.
    ///     <para>
    ///         ⚠ Memoizes the RESULT, never the Task, which is what <c>ChartScoringLevels</c> does and
    ///         why. A cached faulted task is permanent: one transient database error while the first
    ///         bubble on a page resolves would rethrow from every later bubble's
    ///         <c>OnParametersSetAsync</c> for the life of the circuit — hours — killing it on each
    ///         reconnect, on nearly every page of the site. Storing the result instead leaves the slot
    ///         empty on failure, so the next render retries.
    ///     </para>
    /// </summary>
    private async Task<Ends> EndsFor(MixEnum mix, CancellationToken cancellationToken)
    {
        // Held ACROSS the reads, so two bubbles racing on a cold mix issue one pair of queries.
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_byMix.TryGetValue(mix, out var cached)) return cached;
            var hard = (await _reader.GetQualifyingCharts(mix, cancellationToken))
                .Select(c => c.ChartId).ToHashSet();
            var easy = (await _reader.GetMostHeldCharts(mix, cancellationToken))
                .Select(c => c.ChartId).ToHashSet();
            var ends = new Ends(hard, easy);
            _byMix[mix] = ends;
            return ends;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record Ends(IReadOnlySet<Guid> Hard, IReadOnlySet<Guid> Easy);
}
