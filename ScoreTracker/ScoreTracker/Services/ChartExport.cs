using System.Globalization;
using System.Text;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;

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
    public sealed record Column(string Key, bool RequiresUser,
        Func<ChartSearchResult, string> Value);

    private static string Num<T>(T? value, string format = "0.##") where T : struct, IFormattable
    {
        return value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static readonly IReadOnlyList<Column> Columns = new List<Column>
    {
        new("Song", false, r => r.Chart.Song.Name.ToString()),
        new("Artist", false, r => r.Chart.Song.Artist.ToString()),
        new("StepArtist", false, r => r.Chart.StepArtist?.ToString() ?? string.Empty),
        new("Type", false, r => r.Chart.Type.GetShortHand()),
        new("Level", false, r => ((int)r.Chart.Level).ToString(CultureInfo.InvariantCulture)),
        new("Mix", false, r => r.Chart.Mix.GetName()),
        new("DebutMix", false, r => r.DebutMix.GetName()),
        new("LegacyDifficulty", false, r => r.Chart.Slot?.GetName() ?? string.Empty),
        new("SongType", false, r => r.Chart.Song.Type.ToString()),
        new("BPM", false, r => r.Chart.Song.Bpm?.ToString() ?? string.Empty),
        new("DurationSeconds", false,
            r => ((int)r.Chart.Song.Duration.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
        new("NoteCount", false, r => Num(r.Chart.NoteCount, "0")),
        new("NPS", false, r => Num(r.Nps)),
        new("Badges", false, r => string.Join("; ", r.Badges.Select(b => b.DisplayName))),
        new("PassDifficulty", false, r => r.PassDifficulty?.ToString() ?? string.Empty),
        new("ScoreDifficulty", false, r => r.ScoreDifficulty?.ToString() ?? string.Empty),
        new("CommunityVote", false, r => r.CommunityVote?.ToString() ?? string.Empty),
        new("ScoringLevel", false, r => Num(r.ScoringLevel)),
        new("CommunityVoteRating", false, r => Num(r.CommunityVoteRating)),
        new("ScoreCount", false, r => r.ScoreCount.ToString(CultureInfo.InvariantCulture)),
        new("PgCount", false, r => r.PgCount.ToString(CultureInfo.InvariantCulture)),
        new("MyPhoenixScore", true, r => Mine(r, m => Num(m.PhoenixScore, "0"))),
        new("MyPhoenixGrade", true, r => Mine(r, m => m.PhoenixGrade?.GetName() ?? string.Empty)),
        new("MyPhoenixPlate", true, r => Mine(r, m => m.PhoenixPlate?.GetShorthand() ?? string.Empty)),
        new("MyLegacyGrade", true, r => Mine(r, m => m.LegacyGrade?.ToString() ?? string.Empty)),
        new("MyLegacyScore", true, r => Mine(r, m => Num(m.LegacyScore, "0"))),
        new("MyBroken", true, r => Mine(r, m => m.IsBroken ? "true" : "false")),
        new("MyRecordedOn", true,
            r => Mine(r, m => m.RecordedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty))
    };

    public static readonly IReadOnlyList<string> DefaultColumns = new[]
        { "Song", "Type", "Level", "NPS", "Badges", "PassDifficulty" };

    private static string Mine(ChartSearchResult result, Func<ChartSearchMyState, string> value)
    {
        return result.My == null ? string.Empty : value(result.My);
    }

    public static string Write(IEnumerable<ChartSearchResult> results, IReadOnlyList<Column> columns)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Key))));
        foreach (var result in results)
            builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Value(result)))));

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
