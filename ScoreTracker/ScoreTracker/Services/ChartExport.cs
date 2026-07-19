using System.Globalization;
using System.Text;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

public enum ChartExportShape
{
    Grouped,
    PerMix
}

/// <summary>
///     The /Charts CSV export (docs/design/charts-srp.md §2 Export): headers are stable
///     English so community tools can parse them — a convenience surface, deliberately
///     outside the versioned api/* contract. Values are formula-injection escaped. My*
///     columns require the signed-in caller and describe the linked appearance; in the
///     per-mix shape they render only on the linked appearance's row.
/// </summary>
public static class ChartExport
{
    public sealed record Column(string Key, bool RequiresUser,
        Func<ChartSearchResult, ChartMixAppearance, string> Value);

    private static string Num<T>(T? value, string format = "0.##") where T : struct, IFormattable
    {
        return value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static readonly IReadOnlyList<Column> Columns = new List<Column>
    {
        new("Song", false, (r, _) => r.Chart.Song.Name.ToString()),
        new("Artist", false, (r, _) => r.Chart.Song.Artist.ToString()),
        new("StepArtist", false, (r, _) => r.Chart.StepArtist?.ToString() ?? string.Empty),
        new("Type", false, (r, _) => r.Chart.Type.GetShortHand()),
        new("Level", false, (r, a) => a.Level.ToString(CultureInfo.InvariantCulture)),
        new("Mix", false, (r, a) => a.Mix.GetName()),
        new("Mixes", false, (r, _) => string.Join("; ", r.Appearances.Select(x => x.Mix.GetName()))),
        new("DebutMix", false, (r, _) => r.DebutMix.GetName()),
        new("LatestMix", false, (r, _) => r.LatestMix.GetName()),
        new("LevelChange", false, (r, _) => Num(r.LevelChange, "0")),
        new("LegacyDifficulty", false, (r, a) => a.Slot?.GetName() ?? string.Empty),
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
        new("PassRatePercent", false, (r, _) => r.ScoreCount == 0
            ? string.Empty
            : (r.PassCount * 100.0 / r.ScoreCount).ToString("0.#", CultureInfo.InvariantCulture)),
        new("ScoreCount", false, (r, _) => r.ScoreCount.ToString(CultureInfo.InvariantCulture)),
        new("PgCount", false, (r, _) => r.PgCount.ToString(CultureInfo.InvariantCulture)),
        new("MyPhoenixScore", true, (r, a) => OnLinked(r, a, m => Num(m.PhoenixScore, "0"))),
        new("MyPhoenixGrade", true, (r, a) => OnLinked(r, a, m => m.PhoenixGrade?.GetName() ?? string.Empty)),
        new("MyPhoenixPlate", true, (r, a) => OnLinked(r, a, m => m.PhoenixPlate?.GetShorthand() ?? string.Empty)),
        new("MyLegacyGrade", true, (r, a) => OnLinked(r, a, m => m.LegacyGrade?.ToString() ?? string.Empty)),
        new("MyLegacyScore", true, (r, a) => OnLinked(r, a, m => Num(m.LegacyScore, "0"))),
        new("MyBroken", true, (r, a) => OnLinked(r, a, m => m.IsBroken ? "true" : "false")),
        new("MyRecordedOn", true,
            (r, a) => OnLinked(r, a, m => m.RecordedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty))
    };

    public static readonly IReadOnlyList<string> DefaultColumns = new[]
        { "Song", "Type", "Level", "Mixes", "NPS", "Badges", "PassDifficulty", "PassRatePercent" };

    private static string OnLinked(ChartSearchResult result, ChartMixAppearance appearance,
        Func<ChartSearchMyState, string> value)
    {
        return result.My == null || appearance.Mix != result.Chart.Mix ? string.Empty : value(result.My);
    }

    public static string Write(IEnumerable<ChartSearchResult> results, IReadOnlyList<Column> columns,
        ChartExportShape shape)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Key))));
        foreach (var result in results)
        {
            if (shape == ChartExportShape.Grouped)
            {
                var linked = result.Appearances.First(a => a.Mix == result.Chart.Mix);
                builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Value(result, linked)))));
            }
            else
            {
                foreach (var appearance in result.Appearances)
                    builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Value(result, appearance)))));
            }
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
