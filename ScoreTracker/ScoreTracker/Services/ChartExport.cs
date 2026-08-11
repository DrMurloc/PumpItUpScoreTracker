using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The /Charts CSV export (docs/design/charts-srp.md §2 Export): headers are stable
///     English so community tools can parse them — a convenience surface, deliberately
///     outside the versioned api/* contract. Values are formula-injection escaped. My*
///     columns require the signed-in caller and carry that player's record in the
///     searched mix.
/// </summary>
public static class ChartExport
{
    /// <summary>
    ///     Everything a column can read beyond the row itself. Kept off
    ///     <see cref="ChartSearchResult" /> deliberately: the page renders that projection on
    ///     every load and must not pay for reads only the CSV wants.
    /// </summary>
    public sealed record ExportContext(string BaseUrl,
        IReadOnlyDictionary<Guid, int>? PlayCounts = null,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, decimal>>? Metrics = null);

    /// <summary>
    ///     A family of piucenter metrics, ticked as one. Unlike a <see cref="Column" /> this is
    ///     NOT one column — it expands to every metric name in its family, which is why the
    ///     picker draws it differently and prints a multiplier.
    ///     <para>
    ///         Headers carry a <c>pc:</c> prefix and are deliberately <b>unstable</b>: the set is
    ///         whatever piucenter last gave us, so the promise the rest of the file makes does
    ///         not extend here. The dialog says so where a person can read it.
    ///     </para>
    /// </summary>
    public sealed record Bundle(string Key, Func<string, bool> Matches)
    {
        public const string HeaderPrefix = "pc:";

        /// <summary>
        ///     The family's names from the whole catalog, ordinal-sorted — not just the names the
        ///     current filter happens to contain. Two exports of different filters must not
        ///     disagree about the header row.
        /// </summary>
        public IReadOnlyList<string> Expand(IReadOnlyList<string> catalogNames)
        {
            return catalogNames.Where(Matches).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>
    ///     Bookkeeping and the metric that already has a first-class column of its own. Neither
    ///     belongs in a passthrough a person ticks.
    /// </summary>
    private static readonly HashSet<string> NeverExported =
        new(new[] { "data_version", "nps" }, StringComparer.Ordinal);

    public static readonly IReadOnlyList<Bundle> Bundles = new[]
    {
        new Bundle("ChartAnalysis", n => n is "difficulty_prediction" or "sustain_time"
            or "time_under_tension" or "last_segment_is_peak"),
        new Bundle("SkillEmphasis", n => n.StartsWith("badge_fraction:", StringComparison.Ordinal)),
        new Bundle("TopSkills", n => n.StartsWith("top3:", StringComparison.Ordinal)),
        new Bundle("PracticeRanks", n => n.StartsWith("practice_rank:", StringComparison.Ordinal)),
        new Bundle("ChartEnding", n => n.StartsWith("last_segment_badge:", StringComparison.Ordinal)),
        new Bundle("RarePatterns", n => n.StartsWith("rare:", StringComparison.Ordinal))
    };

    /// <summary>
    ///     Every catalog metric name a bundle is allowed to emit. No catalog is an empty one:
    ///     an absent passthrough hides the group rather than breaking the picker.
    /// </summary>
    public static IReadOnlyList<string> ExportableMetricNames(IEnumerable<string>? catalogNames)
    {
        return catalogNames?.Where(n => !NeverExported.Contains(n)).ToArray() ?? Array.Empty<string>();
    }

    /// <summary>
    ///     Which mixes a column means anything on. Both the dialog and the endpoint resolve
    ///     through <see cref="ColumnsFor" />, so a column the picker hides can never arrive in
    ///     the file through a saved setting.
    /// </summary>
    public enum Scope
    {
        Always,
        PhoenixFamily,
        LegacyFamily,

        /// <summary>Phoenix 2 alone: the only mix whose journal is a gap-free play log.</summary>
        Phoenix2Only
    }

    public sealed record Column(string Key, bool RequiresUser,
        Func<ChartSearchResult, ExportContext, string> Value, Scope Scope = Scope.Always);

    /// <summary>The columns that carry meaning in this mix, in registry order.</summary>
    public static IReadOnlyList<Column> ColumnsFor(MixEnum mix)
    {
        var legacy = mix.UsesLegacyScoring();
        return Columns.Where(c => c.Scope switch
        {
            Scope.PhoenixFamily => !legacy,
            Scope.LegacyFamily => legacy,
            Scope.Phoenix2Only => mix == MixEnum.Phoenix2,
            _ => true
        }).ToArray();
    }

    private static string Num<T>(T? value, string format = "0.##") where T : struct, IFormattable
    {
        return value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    ///     Built once per mix — the configuration allocates several dictionaries, and an
    ///     unpaged export prices four thousand rows through it.
    ///     <para>
    ///         includeCoOp is true because Phoenix 1 CO-OP charts really do earn PUMBILITY.
    ///         Phoenix 2 needs no opinion here: its configuration zeroes CO-OP itself, so the
    ///         argument reaches nothing.
    ///     </para>
    /// </summary>
    private static readonly ConcurrentDictionary<MixEnum, ScoringConfiguration> PumbilityConfigs = new();

    private static ScoringConfiguration PumbilityFor(MixEnum mix)
    {
        return PumbilityConfigs.GetOrAdd(mix, m => ScoringConfiguration.PumbilityScoring(m, true));
    }

    public static readonly IReadOnlyList<Column> Columns = new List<Column>
    {
        // The join key. The export exists so tools can parse it and had none: song plus
        // type plus level is not stable across a rename.
        new("ChartId", false, (r, _) => r.Chart.Id.ToString()),
        new("Song", false, (r, _) => r.Chart.Song.Name.ToString()),
        new("ChartUrl", false, (r, c) => c.BaseUrl + r.Chart.CanonicalPath()),
        new("Artist", false, (r, _) => r.Chart.Song.Artist.ToString()),
        new("StepArtist", false, (r, _) => r.Chart.StepArtist?.ToString() ?? string.Empty),
        new("Type", false, (r, _) => r.Chart.Type.GetShortHand()),
        // 1 for everything that is not a CO-OP chart, which is the truth rather than a blank.
        new("PlayerCount", false, (r, _) => r.Chart.PlayerCount.ToString(CultureInfo.InvariantCulture)),
        new("Level", false, (r, _) => ((int)r.Chart.Level).ToString(CultureInfo.InvariantCulture)),
        new("Mix", false, (r, _) => r.Chart.Mix.GetName()),
        new("DebutMix", false, (r, _) => r.DebutMix.GetName()),
        new("LegacyDifficulty", false, (r, _) => r.Chart.Slot?.GetName() ?? string.Empty),
        new("SongType", false, (r, _) => r.Chart.Song.Type.ToString()),
        new("BPM", false, (r, _) => r.Chart.Song.Bpm?.ToString() ?? string.Empty),
        new("DurationSeconds", false,
            (r, _) => ((int)r.Chart.Song.Duration.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
        new("NoteCount", false, (r, _) => Num(r.Chart.NoteCount, "0")),
        new("NPS", false, (r, _) => Num(r.Nps)),
        new("Badges", false, (r, _) => string.Join("; ", r.Badges.Select(b => b.DisplayName))),
        new("PassDifficulty", false, (r, _) => r.PassDifficulty?.ToString() ?? string.Empty),
        new("ScoreDifficulty", false, (r, _) => r.ScoreDifficulty?.ToString() ?? string.Empty),
        new("CommunityVote", false, (r, _) => r.CommunityVote?.ToString() ?? string.Empty),
        new("ScoringLevel", false, (r, _) => Num(r.ScoringLevel)),
        new("CommunityVoteRating", false, (r, _) => Num(r.CommunityVoteRating)),
        new("ScoreCount", false, (r, _) => r.ScoreCount.ToString(CultureInfo.InvariantCulture)),
        new("PgCount", false, (r, _) => r.PgCount.ToString(CultureInfo.InvariantCulture)),
        // What this chart's record is worth under the mix's PUMBILITY formula, whether or not
        // it is in the player's top fifty. Blank means no record; 0.00 means the formula
        // genuinely prices it at nothing (Phoenix 2 pays zero below level 10, and neither mix
        // pays for a break). Two decimals here and only here — Web is the layer that rounds.
        new("Pumbility", true, (r, _) => Mine(r, m => m.PhoenixScore == null
            ? string.Empty
            : PumbilityFor(r.Chart.Mix)
                .GetScore(r.Chart, m.PhoenixScore.Value, m.PhoenixPlate ?? PhoenixPlate.RoughGame, m.IsBroken)
                .ToString("0.00", CultureInfo.InvariantCulture)), Scope.PhoenixFamily),
        // Family-scoped: a Phoenix column on an XX search only ever produced a blank column,
        // and offering it read as a bug rather than as absence.
        new("MyPhoenixScore", true, (r, _) => Mine(r, m => Num(m.PhoenixScore, "0")), Scope.PhoenixFamily),
        new("MyPhoenixGrade", true, (r, _) => Mine(r, m => m.PhoenixGrade?.GetName() ?? string.Empty),
            Scope.PhoenixFamily),
        new("MyPhoenixPlate", true, (r, _) => Mine(r, m => m.PhoenixPlate?.GetShorthand() ?? string.Empty),
            Scope.PhoenixFamily),
        new("MyLegacyGrade", true, (r, _) => Mine(r, m => m.LegacyGrade?.ToString() ?? string.Empty),
            Scope.LegacyFamily),
        new("MyLegacyScore", true, (r, _) => Mine(r, m => Num(m.LegacyScore, "0")), Scope.LegacyFamily),
        // The breakdown of the play that set the record. Null where it was never observed —
        // manual and CSV entries never carry one, and an import only attaches one when the
        // producing play was still on the recently-played list.
        new("MyPerfects", true, (r, _) => Mine(r, m => Num(m.Judgements?.Perfects, "0")), Scope.PhoenixFamily),
        new("MyGreats", true, (r, _) => Mine(r, m => Num(m.Judgements?.Greats, "0")), Scope.PhoenixFamily),
        new("MyGoods", true, (r, _) => Mine(r, m => Num(m.Judgements?.Goods, "0")), Scope.PhoenixFamily),
        new("MyBads", true, (r, _) => Mine(r, m => Num(m.Judgements?.Bads, "0")), Scope.PhoenixFamily),
        new("MyMisses", true, (r, _) => Mine(r, m => Num(m.Judgements?.Misses, "0")), Scope.PhoenixFamily),
        // Solved from the score and the breakdown, since no PIU surface reports it. Null
        // unless the judgements cover the whole chart — see PhoenixComboSolver.
        new("MyMaxCombo", true, (r, _) => Mine(r,
            m => Num(PhoenixComboSolver.MaxComboFor(m.Judgements,
                m.PhoenixScore == null ? null : (PhoenixScore)m.PhoenixScore.Value, r.Chart.NoteCount), "0")),
            Scope.PhoenixFamily),
        // Journal rows for this chart. Absent from the sidecar means no rows at all, which is
        // 0 rather than blank — the player holds a record here but nothing journaled it.
        new("MyPlayCount", true, (r, c) => c.PlayCounts == null
            ? string.Empty
            : (c.PlayCounts.TryGetValue(r.Chart.Id, out var plays) ? plays : 0)
            .ToString(CultureInfo.InvariantCulture), Scope.Phoenix2Only),
        new("MyBroken", true, (r, _) => Mine(r, m => m.IsBroken ? "true" : "false")),
        new("MyRecordedOn", true,
            (r, _) => Mine(r, m => m.RecordedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty))
    };

    public static readonly IReadOnlyList<string> DefaultColumns = new[]
        { "Song", "Type", "Level", "NPS", "Badges", "PassDifficulty" };

    private static string Mine(ChartSearchResult result, Func<ChartSearchMyState, string> value)
    {
        return result.My == null ? string.Empty : value(result.My);
    }

    public static string Write(IEnumerable<ChartSearchResult> results, IReadOnlyList<Column> columns,
        ExportContext context, IReadOnlyList<string>? metricNames = null)
    {
        var builder = new StringBuilder();
        var metrics = metricNames ?? Array.Empty<string>();
        builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Key))
            .Concat(metrics.Select(n => Escape(Bundle.HeaderPrefix + n)))));

        foreach (var result in results)
        {
            var banked = context.Metrics != null && context.Metrics.TryGetValue(result.Chart.Id, out var m)
                ? m
                : null;
            builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Value(result, context)))
                .Concat(metrics.Select(n => banked != null && banked.TryGetValue(n, out var v)
                    ? Escape(v.ToString("0.####", CultureInfo.InvariantCulture))
                    : string.Empty))));
        }

        return builder.ToString();
    }

    /// <summary>RFC-4180 quoting plus the Excel formula-injection guard (=, +, -, @ starts).</summary>
    internal static string Escape(string value)
    {
        if (value.Length == 0) return value;
        if (value[0] is '=' or '+' or '-' or '@') value = "'" + value;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
