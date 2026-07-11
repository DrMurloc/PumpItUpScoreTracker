using System.Globalization;
using System.Text;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     The skill flip's policy: turns a chart's banked piucenter metrics into our
///     <see cref="Skill" /> tags. Their top-3 summary maps to highlighted tags; badge
///     fractions past the threshold map to contains tags; Fast/Slow derive from NPS
///     quartiles within the chart's (type, level) folder; EndRun derives from a run
///     badge on the final segment of a chart that isn't run-dominant overall.
///     Gimmicks / VeryFast / Moderate have no measurable counterpart and are never
///     emitted — functionally retired (members linger until a post-flip cleanup).
/// </summary>
internal static class PiuCenterSkillMapper
{
    // Tunable constants — baselined 2026-07-11 against the archived hand tags and the
    // 050726 corpus (see the PR's calibration notes).
    internal const decimal BadgeFractionThreshold = 0.30m;
    internal const double FastNpsQuantile = 0.75;
    internal const double SlowNpsQuantile = 0.25;

    private static readonly string[] RunSkills = { "run", "anchor_run", "run_without_twists" };

    private static readonly IReadOnlyDictionary<string, Skill[]> TheirsToOurs =
        new Dictionary<string, Skill[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["run"] = new[] { Skill.Runs },
            ["anchor_run"] = new[] { Skill.Runs },
            ["run_without_twists"] = new[] { Skill.Runs },
            ["drill"] = new[] { Skill.Drills },
            ["jump"] = new[] { Skill.Jumps },
            ["jack"] = new[] { Skill.Jacks },
            ["bracket"] = new[] { Skill.Brackets },
            ["staggered_bracket"] = new[] { Skill.Brackets },
            ["bracket_run"] = new[] { Skill.BracketsAndRuns },
            ["bracket_drill"] = new[] { Skill.Brackets, Skill.Drills },
            ["bracket_jump"] = new[] { Skill.Brackets, Skill.Jumps },
            ["bracket_twist"] = new[] { Skill.Brackets, Skill.Twists },
            ["twists"] = new[] { Skill.Twists },
            ["twist_90"] = new[] { Skill.Twists },
            ["twist_over90"] = new[] { Skill.Twists },
            ["twist_close"] = new[] { Skill.Twists },
            ["twist_far"] = new[] { Skill.Twists },
            ["mid6_doubles"] = new[] { Skill.HalfDouble },
            ["mid4_doubles"] = new[] { Skill.HalfDouble },
            ["sustained"] = new[] { Skill.Stamina },
            ["bursty"] = new[] { Skill.Bursts },
            ["doublestep"] = new[] { Skill.Technical },
            ["footswitch"] = new[] { Skill.Technical },
            ["hold_footswitch"] = new[] { Skill.Technical },
            ["hold_footslide"] = new[] { Skill.Technical },
            ["side3_singles"] = new[] { Skill.Technical },
            ["5-stair"] = new[] { Skill.Technical },
            ["10-stair"] = new[] { Skill.Technical },
            ["yog_walk"] = new[] { Skill.Technical },
            ["cross-pad_transition"] = new[] { Skill.Technical },
            ["co-op_pad_transition"] = new[] { Skill.Technical },
            ["split"] = new[] { Skill.Technical },
            ["hands"] = new[] { Skill.Technical }
        };

    public static ChartSkillsRecord Map(Guid chartId, IReadOnlyList<ChartSkillMetric> chartMetrics,
        decimal? fastNpsCutoff, decimal? slowNpsCutoff)
    {
        var highlights = new HashSet<Skill>();
        var contains = new HashSet<Skill>();
        decimal? nps = null;
        var endsOnRun = false;
        var runDominant = false;

        foreach (var metric in chartMetrics)
            if (metric.MetricName.StartsWith(PiuCenterMetrics.Top3Prefix, StringComparison.Ordinal))
            {
                var theirs = metric.MetricName[PiuCenterMetrics.Top3Prefix.Length..];
                AddMapped(highlights, theirs);
                if (RunSkills.Contains(theirs)) runDominant = true;
            }
            else if (metric.MetricName.StartsWith(PiuCenterMetrics.BadgeFractionPrefix, StringComparison.Ordinal))
            {
                if (metric.Value >= BadgeFractionThreshold)
                    AddMapped(contains, metric.MetricName[PiuCenterMetrics.BadgeFractionPrefix.Length..]);
            }
            else if (metric.MetricName.StartsWith(PiuCenterMetrics.LastSegmentPrefix, StringComparison.Ordinal))
            {
                if (RunSkills.Contains(metric.MetricName[PiuCenterMetrics.LastSegmentPrefix.Length..]))
                    endsOnRun = true;
            }
            else if (metric.MetricName == PiuCenterMetrics.Nps)
            {
                nps = metric.Value;
            }

        if (endsOnRun && !runDominant) contains.Add(Skill.EndRun);
        if (nps != null && fastNpsCutoff != null && nps >= fastNpsCutoff) contains.Add(Skill.Fast);
        if (nps != null && slowNpsCutoff != null && nps <= slowNpsCutoff) contains.Add(Skill.Slow);

        contains.ExceptWith(highlights);
        return new ChartSkillsRecord(chartId, contains, highlights);
    }

    /// <summary>Normalization shared with the alias auto-matcher: fold diacritics, drop everything but letters and digits.</summary>
    public static string Normalize(string value)
    {
        var folded = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(folded.Length);
        foreach (var ch in folded)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static void AddMapped(ISet<Skill> target, string theirSkill)
    {
        if (!TheirsToOurs.TryGetValue(theirSkill, out var ours)) return;
        foreach (var skill in ours) target.Add(skill);
    }
}
