using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Whether a chart is in the week's Hardmode list, and whether this viewer wants to see it
///     marked (docs/design/hardmode-leaderboard.md D24). Circuit-scoped and memoized per mix,
///     exactly like <see cref="ChartScoringLevels" />: a page of forty bubbles must not ask forty
///     times, and the bubble already awaits that service on the same render.
///     <para>
///         Reads the list through the Domain port rather than ChartIntelligence's contract — the
///         port exists for precisely this ("the next surface to want the list", D13) and the
///         reader behind it is cached, so this is one query per circuit at worst.
///     </para>
/// </summary>
public sealed class HardmodeCharts
{
    /// <summary>
    ///     Off means "do not mark"; the setting's ABSENCE means on, because the mark defaults on
    ///     for everyone including signed-out visitors (D24). Storing only the opt-out keeps the
    ///     default free — nothing has to be written for the common case.
    /// </summary>
    public const string SettingKey = "Universal__HideHardmodeMark";

    private readonly ICurrentUserAccessor _currentUser;
    private readonly IHardmodeChartReader _reader;
    private readonly IUiSettingsAccessor _settings;
    private readonly Dictionary<MixEnum, IReadOnlySet<Guid>> _byMix = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool? _enabled;

    public HardmodeCharts(IHardmodeChartReader reader, IUiSettingsAccessor settings,
        ICurrentUserAccessor currentUser)
    {
        _reader = reader;
        _settings = settings;
        _currentUser = currentUser;
    }

    /// <summary>
    ///     Whether this viewer marks Hardmode charts at all. Defaults on, and a signed-out
    ///     visitor has no settings to read — they get the default like anyone else.
    ///     <para>
    ///         ⚠ The signed-out branch must SHORT-CIRCUIT rather than ask the settings accessor.
    ///         Its anonymous path is ProtectedBrowserStorage, which is JS interop and throws
    ///         outside a live circuit — and the difficulty bubble renders on statically-rendered
    ///         pages (the chart page, the weekly charts page), so asking there 500s the page for
    ///         every signed-out visitor. <c>ScoreColorPreferences</c> guards the same way for the
    ///         same reason.
    ///     </para>
    /// </summary>
    public async Task<bool> IsEnabled()
    {
        if (!_currentUser.IsLoggedIn) return true;
        // The result, not the task - same reason as Qualifying below.
        return _enabled ??= await _settings.GetSetting(SettingKey) is null;
    }

    /// <summary>
    ///     Whether the chart is in the mix's Hardmode list AND this viewer wants it marked. One
    ///     call answers the bubble's whole question, so a caller cannot get the two halves out of
    ///     step. False on any mix without a census, which is every mix but Phoenix 2 today.
    /// </summary>
    public async Task<bool> IsMarked(MixEnum mix, Guid chartId, CancellationToken cancellationToken = default)
    {
        if (!await IsEnabled()) return false;
        return (await Qualifying(mix, cancellationToken)).Contains(chartId);
    }

    /// <summary>
    ///     The mix's list, read once per circuit.
    ///     <para>
    ///         ⚠ Memoizes the RESULT, never the Task, which is what <c>ChartScoringLevels</c> does
    ///         and why. A cached faulted task is permanent: one transient database error while the
    ///         first bubble on a page resolves would rethrow from every later bubble's
    ///         <c>OnParametersSetAsync</c> for the life of the circuit — hours — killing it on
    ///         each reconnect, on nearly every page of the site now that the glow is everywhere.
    ///         Storing the result instead leaves the slot empty on failure, so the next render
    ///         retries. It also means the read cannot inherit the first caller's cancellation.
    ///     </para>
    /// </summary>
    private async Task<IReadOnlySet<Guid>> Qualifying(MixEnum mix, CancellationToken cancellationToken)
    {
        // The lock is held ACROSS the read, so two bubbles racing on a cold mix issue one query
        // rather than two. It is one query per circuit, so the contention is a non-issue and the
        // alternative — releasing first — is what reintroduces the double read.
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_byMix.TryGetValue(mix, out var cached)) return cached;
            var charts = await _reader.GetQualifyingCharts(mix, cancellationToken);
            var ids = charts.Select(c => c.ChartId).ToHashSet();
            _byMix[mix] = ids;
            return ids;
        }
        finally
        {
            _lock.Release();
        }
    }

}
