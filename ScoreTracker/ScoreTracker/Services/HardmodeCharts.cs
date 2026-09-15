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

    private readonly IHardmodeChartReader _reader;
    private readonly IUiSettingsAccessor _settings;
    private readonly Dictionary<MixEnum, Task<IReadOnlySet<Guid>>> _byMix = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Task<bool>? _enabled;

    public HardmodeCharts(IHardmodeChartReader reader, IUiSettingsAccessor settings)
    {
        _reader = reader;
        _settings = settings;
    }

    /// <summary>
    ///     Whether this viewer marks Hardmode charts at all. Defaults on, and a signed-out
    ///     visitor has no settings to read — they get the default like anyone else.
    /// </summary>
    public Task<bool> IsEnabled()
    {
        return _enabled ??= ReadEnabled();
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

    private async Task<IReadOnlySet<Guid>> Qualifying(MixEnum mix, CancellationToken cancellationToken)
    {
        // The lock guards the DICTIONARY, not the read: several bubbles render concurrently on
        // one circuit, and two of them racing to add the same mix would both issue the query.
        await _lock.WaitAsync(cancellationToken);
        Task<IReadOnlySet<Guid>> task;
        try
        {
            if (!_byMix.TryGetValue(mix, out var existing))
            {
                existing = Read(mix, cancellationToken);
                _byMix[mix] = existing;
            }

            task = existing;
        }
        finally
        {
            _lock.Release();
        }

        return await task;
    }

    private async Task<IReadOnlySet<Guid>> Read(MixEnum mix, CancellationToken cancellationToken)
    {
        var charts = await _reader.GetQualifyingCharts(mix, cancellationToken);
        return charts.Select(c => c.ChartId).ToHashSet();
    }

    private async Task<bool> ReadEnabled()
    {
        return await _settings.GetSetting(SettingKey) is null;
    }
}
