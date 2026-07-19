using Microsoft.AspNetCore.Http;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Theming;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The /Charts query-string contract, parsed server-side for the CSV export — the same
///     parameter names (and old-page read aliases) the page itself binds, so a filtered
///     page URL IS an export request. The page parses through Blazor's query binding; the
///     E2E round-trip fact is what pins the two against drift.
/// </summary>
public static class ChartSearchUrlParser
{
    public static SearchChartsQuery Parse(IQueryCollection query, MixEnum mix, Guid? userId)
    {
        return new SearchChartsQuery
        {
            Mix = mix,
            AllMixes = string.Equals(Str(query, "Scope"), "all", StringComparison.OrdinalIgnoreCase),
            UserId = userId,
            SongNameContains = Str(query, "Song") ?? Str(query, "SongName"),
            LevelMin = Int(query, "LevelMin") ?? Int(query, "Difficulty"),
            LevelMax = Int(query, "LevelMax") ?? Int(query, "Difficulty"),
            Types = CsvEnum<ChartType>(Str(query, "Type") ?? Str(query, "ChartType")),
            SongTypes = CsvEnum<SongType>(Str(query, "SongType")),
            Artists = Csv(Str(query, "Artist") ?? Str(query, "SongArtist")),
            StepArtists = Csv(Str(query, "StepArtist")),
            BpmMin = Dec(query, "BpmMin"),
            BpmMax = Dec(query, "BpmMax"),
            NpsMin = Dec(query, "NpsMin"),
            NpsMax = Dec(query, "NpsMax"),
            NoteCountMin = Int(query, "NoteMin"),
            NoteCountMax = Int(query, "NoteMax"),
            Badges = Csv(Str(query, "Badges")),
            DebutMixes = CsvEnum<MixEnum>(Str(query, "DebutMix")),
            AvailableIn = EnumValue<MixEnum>(Str(query, "In")),
            NotAvailableIn = EnumValue<MixEnum>(Str(query, "NotIn")),
            ReratedUp = Bool(query, "RUp"),
            ReratedDown = Bool(query, "RDown"),
            LegacySlots = CsvEnum<LegacySlot>(Str(query, "Slots")),
            PassDifficulty = CsvEnum<TierListCategory>(Str(query, "PassDiff")),
            ScoreDifficulty = CsvEnum<TierListCategory>(Str(query, "ScoreDiff")),
            CommunityVote = CsvEnum<TierListCategory>(Str(query, "Vote")),
            PassRateMin = Int(query, "PassRateMin") is { } rate ? rate / 100.0 : null,
            ScoringLevelMin = Dbl(query, "SlMin"),
            ScoringLevelMax = Dbl(query, "SlMax"),
            ScoreState = EnumValue<ChartScoreStateFilter>(Str(query, "State")),
            PhoenixGradeMin = EnumValue<PhoenixLetterGrade>(Str(query, "PhxGrade")),
            PhoenixPlateMin = EnumValue<PhoenixPlate>(Str(query, "PhxPlate")),
            PhoenixScoreMin = Int(query, "PhxMin"),
            PhoenixScoreMax = Int(query, "PhxMax"),
            LegacyGradeMin = EnumValue<XXLetterGrade>(Str(query, "LegGrade")),
            RecordedFrom = Date(query, "RecFrom"),
            RecordedTo = Date(query, "RecTo") is { } to ? to.AddDays(1).AddTicks(-1) : null,
            // Same target the page's switch uses: the newest themed mix.
            NotReclearedIn = Bool(query, "Reclear")
                ? MixThemes.ThemedMixes.OrderBy(m => m.DisplayOrder()).Last()
                : null,
            Sort = EnumValue<ChartSearchSort>(Str(query, "Sort")) ?? ChartSearchSort.Level,
            SortDescending = Str(query, "Dir")?.ToLowerInvariant() != "asc",
            Page = null
        };
    }

    private static string? Str(IQueryCollection query, string key)
    {
        return query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.ToString() : null;
    }

    private static int? Int(IQueryCollection query, string key)
    {
        return int.TryParse(Str(query, key), out var value) ? value : null;
    }

    private static decimal? Dec(IQueryCollection query, string key)
    {
        return decimal.TryParse(Str(query, key), out var value) ? value : null;
    }

    private static double? Dbl(IQueryCollection query, string key)
    {
        return double.TryParse(Str(query, key), out var value) ? value : null;
    }

    private static bool Bool(IQueryCollection query, string key)
    {
        return bool.TryParse(Str(query, key), out var value) && value;
    }

    private static DateTimeOffset? Date(IQueryCollection query, string key)
    {
        return DateTime.TryParse(Str(query, key), out var value)
            ? new DateTimeOffset(value, TimeSpan.Zero)
            : null;
    }

    private static T? EnumValue<T>(string? value) where T : struct, Enum
    {
        return Enum.TryParse<T>(value, true, out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string>? Csv(string? csv)
    {
        var values = csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values is { Length: > 0 } ? values : null;
    }

    private static IReadOnlyList<T>? CsvEnum<T>(string? csv) where T : struct, Enum
    {
        var values = csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => EnumValue<T>(v))
            .Where(v => v != null)
            .Select(v => v!.Value)
            .Distinct()
            .ToArray();
        return values is { Length: > 0 } ? values : null;
    }
}
