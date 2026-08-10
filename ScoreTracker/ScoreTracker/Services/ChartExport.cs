using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

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
    public sealed record ExportContext(string BaseUrl);

    /// <summary>
    ///     Which mixes a column means anything on. Both the dialog and the endpoint resolve
    ///     through <see cref="ColumnsFor" />, so a column the picker hides can never arrive in
    ///     the file through a saved setting.
    /// </summary>
    public enum Scope
    {
        Always,
        PhoenixFamily,
        LegacyFamily
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
        ExportContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Key))));
        foreach (var result in results)
            builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Value(result, context)))));

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
