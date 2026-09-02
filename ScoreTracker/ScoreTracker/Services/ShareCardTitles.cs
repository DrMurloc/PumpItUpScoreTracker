using System.Text;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The share card's header, composed by one rule (design doc §9): the **title** says what
///     the rows are, the **subtitle** says how to read them, and the **stamp** says whose
///     reading it is — and prints nothing when the title already said so. Both Download
///     buttons compose through here, so the two pictures name themselves the same way, and
///     the filename carries the same subject so two downloads of one folder never collide.
/// </summary>
public static class ShareCardTitles
{
    public sealed record Header(string Title, string Subtitle, string? Stamp);

    /// <summary>What a tier-list download's rows are: the lens's tiers, the player's own scores, or the folder's speed bands.</summary>
    public enum TierListView
    {
        Tier,
        MyScores,
        Speed
    }

    /// <param name="folder">"Singles 20", "CoOp ×3" — the folder, already localized.</param>
    /// <param name="lensName">The lens's display name; the subject on a tier view, the shown difficulty elsewhere.</param>
    /// <param name="grouping">The My Scores grouping's display name; unused on the other views.</param>
    public static Header TierList(string folder, TierListView view, string lensName, bool personalized,
        string playerTag, string? grouping, string mixName, string date, Func<string, string> localize)
    {
        var shown = $"{localize("Shown Difficulty")}: {lensName} · {mixName} · {date}";
        return view switch
        {
            TierListView.MyScores => new Header(
                $"{folder} — {string.Format(localize("{0}'s Scores by {1}"), playerTag, grouping)}", shown, null),
            TierListView.Speed => new Header($"{folder} — {localize("Speed")}", shown, null),
            _ => new Header($"{folder} — {lensName}", $"{mixName} · {date}",
                personalized ? string.Format(localize("Personalized for {0}"), playerTag) : localize("Crowd sourced"))
        };
    }

    /// <param name="poolLabel">"Singles pool" / "All pools" on Phoenix 2, null where the mix has one pool.</param>
    public static Header Targets(bool poolLens, string groupingName, string energyLabel, string? poolLabel,
        bool gainsOnly, bool phoenix1Projected, string mixName, string date, string playerTag,
        Func<string, string> localize)
    {
        var title = poolLens
            ? $"{localize("PUMBILITY Pool")} — {localize("Top 50")}"
            : $"{localize("PUMBILITY Targets")} — {groupingName}";
        var clarifiers = new List<string> { $"{localize("Energy")}: {energyLabel}" };
        if (poolLabel != null) clarifiers.Add(poolLabel);
        if (gainsOnly) clarifiers.Add(localize("Only projected PUMBILITY gains"));
        if (phoenix1Projected) clarifiers.Add(localize("Phoenix 1 projected"));
        clarifiers.Add(mixName);
        clarifiers.Add(date);
        return new Header(title, string.Join(" · ", clarifiers),
            string.Format(localize("Personalized for {0}"), playerTag));
    }

    /// <param name="subject">The unlocalized subject key — "Pass", "PersonalizedScore", "ScoresByAge", "Speed".</param>
    public static string TierListFileName(MixEnum mix, ChartType type, int level, string subject, string date)
    {
        return $"TierList_{mix}_{type}{level}_{Slug(subject)}_{date}.png";
    }

    public static string TargetsFileName(MixEnum mix, string grouping, string energy, string pool, string date)
    {
        return $"PumbilityTargets_{mix}_{Slug(grouping)}_{Slug(energy)}_{Slug(pool)}_{date}.png";
    }

    /// <summary>Letters and digits only — a filename segment, never a sentence.</summary>
    private static string Slug(string value)
    {
        var slug = new StringBuilder(value.Length);
        foreach (var c in value)
            if (char.IsLetterOrDigit(c))
                slug.Append(c);
        return slug.Length > 0 ? slug.ToString() : "List";
    }
}
