using System.Text;
using Microsoft.Extensions.Localization;
using ScoreTracker.Domain.Records;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Theming;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The session's share image (docs/design/march-of-murlocs.md D25): the tier-list card, its
///     rows the twenty-minute sections of the session, its tiles composed by the same
///     ShareCardComposer every other card uses under the same remembered options — with the
///     session points in the corner where the tier-list card prints PUMBILITY, and the three
///     options that mean nothing on a session (To Do, Top 50, Passed in other mixes) forced off.
///     The renderer is the shared SkiaShareCardRenderer; nothing here draws.
/// </summary>
public static class MoMShareCardComposer
{
    /// <summary>The options a session card is drawn under: the remembered ones, less the three that do not apply (D25).</summary>
    public static ShareCardOptions ForSession(ShareCardOptions options)
    {
        return options with { BoundaryTodo = false, BoundaryOtherMixes = false, BoundaryTop50 = false, ExpectedGains = false };
    }

    public static TierListShareCard Compose(MoMSessionView view, ShareCardOptions options, MixPalette palette,
        string date, IStringLocalizer<App> localizer)
    {
        var o = ForSession(options);
        var rows = MoMSections.Group(view.Charts)
            .Select(section => new TierListShareCard.Row(SectionName(section, localizer), palette.Primary,
                section.Charts.Select(t => Tile(t, o, view.Mix, palette)).ToArray()))
            .ToArray();
        return Card(view, o, palette, date, localizer, rows);
    }

    /// <summary>The dialog's example: the first few charts as one row, under the options the download will use.</summary>
    public static TierListShareCard? Sample(MoMSessionView view, ShareCardOptions options, MixPalette palette,
        string date, IStringLocalizer<App> localizer)
    {
        if (view.Charts.Count == 0) return null;
        var o = ForSession(options);
        var row = new TierListShareCard.Row(localizer["Example"], palette.Primary,
            view.Charts.Take(ShareCardSample.Size).Select(t => Tile(t, o, view.Mix, palette)).ToArray());
        return Card(view, o, palette, date, localizer, new[] { row });
    }

    public static TierListShareCard.Tile Tile(MoMTimedChart timed, ShareCardOptions options, MixEnum mix, MixPalette palette)
    {
        var chart = timed.Chart;
        var facts = new ShareCardComposer.TileFacts(chart.Chart, chart.Score, chart.Plate,
            Passed: !chart.IsBroken, Broken: chart.IsBroken, IsToDo: false, PassedInOtherMix: false,
            InTop50Type: false, InTop50Combined: false, Gain: null, ExpectedScore: null, CurrentPumbility: null,
            Skills: null, BubbleUrl: ShareCardImages.DifficultyBubble(chart.Chart));
        var tile = ShareCardComposer.Compose(facts, options, mix, palette);
        // The corner is the session's currency: points, whole, in the primary — where the
        // tier-list card prints a PUMBILITY value.
        return options.Pumbility
            ? tile with { CornerLabel = chart.SessionScore.ToString("N0"), CornerHex = palette.Primary }
            : tile;
    }

    public static string SectionName(MoMSections.MoMSection section, IStringLocalizer<App> localizer)
    {
        return section.ToMinute is { } to
            ? localizer["{0}–{1} min", section.FromMinute, to]
            : localizer["{0} min +", section.FromMinute];
    }

    public static string FileName(MoMSessionView view, string date)
    {
        var type = view.ChartType == ChartType.Double ? "Doubles" : view.ChartType.ToString();
        return $"MarchOfMurlocs_{view.Mix}_{Slug(view.Season.Name)}_{type}_{Slug(view.Player?.Name.ToString() ?? "Session")}_{date}.png";
    }

    private static TierListShareCard Card(MoMSessionView view, ShareCardOptions o, MixPalette palette, string date,
        IStringLocalizer<App> localizer, IReadOnlyList<TierListShareCard.Row> rows)
    {
        var type = view.ChartType == ChartType.Double ? localizer["Doubles"] : localizer["Singles"];
        var mixName = view.Mix == MixEnum.Phoenix2 ? "Phoenix 2" : "Phoenix";
        var player = view.Player?.Name.ToString() ?? localizer["Unknown player"];
        var stamp = view.IsDraft
            ? localizer["Draft"].Value
            : $"{MoMText.Ordinal(view.Place, localizer)} {localizer["of {0}", view.Of]}";
        return new TierListShareCard(
            $"March of Murlocs {view.Season.Name} — {type}",
            $"{player} · {localizer["{0} points", view.TotalScore.ToString("N0")]} · {localizer["{0} charts", view.Levers.ChartsPlayed]} · {mixName} · {date}",
            stamp,
            palette.Primary, palette.Background, palette.Surface, palette.Ink, palette.InkMuted,
            $"https://piuscores.arroweclip.se{MoMText.SessionPath(view.SessionId)}",
            null,
            rows,
            ShareCardComposer.Legend(o, view.Mix, palette, key => localizer[key].Value));
    }

    private static string Slug(string value)
    {
        var slug = new StringBuilder(value.Length);
        foreach (var c in value)
            if (char.IsLetterOrDigit(c))
                slug.Append(c);
        return slug.Length > 0 ? slug.ToString() : "Session";
    }
}
