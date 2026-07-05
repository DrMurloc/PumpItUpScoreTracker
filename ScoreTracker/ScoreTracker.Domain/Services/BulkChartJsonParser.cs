using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     Parses the admin "bulk add charts" JSON blob (schema: docs/design/new-charts-json.md)
///     into validated song/chart specs. Every invalid input surfaces as a per-song (or global)
///     error message — value-type construction failures are caught, never thrown to the caller.
/// </summary>
public sealed class BulkChartJsonParser : IBulkChartJsonParser
{
    public const string DefaultChannelName = "PUMP IT UP Official";
    public const int MaxDurationSeconds = 3600;

    private static readonly ChartType[] AllowedChartTypes = { ChartType.Single, ChartType.Double, ChartType.CoOp };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly Regex YoutubeHashRegex = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    public BulkChartsParseResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new BulkChartsParseResult(Array.Empty<BulkSongParseResult>(),
                new[] { "Input is empty — paste a JSON blob." });

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
                { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            return new BulkChartsParseResult(Array.Empty<BulkSongParseResult>(),
                new[] { $"Invalid JSON: {ex.Message}" });
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryGetPropertyIgnoreCase(document.RootElement, "songs", out var songsElement))
                return new BulkChartsParseResult(Array.Empty<BulkSongParseResult>(),
                    new[] { "Top-level JSON must be an object with a \"songs\" array." });

            if (songsElement.ValueKind != JsonValueKind.Array)
                return new BulkChartsParseResult(Array.Empty<BulkSongParseResult>(),
                    new[] { "\"songs\" must be an array." });

            var entries = songsElement.EnumerateArray()
                .Select((element, index) => ParseSong(element, index + 1))
                .ToArray();

            if (!entries.Any())
                return new BulkChartsParseResult(Array.Empty<BulkSongParseResult>(),
                    new[] { "\"songs\" array is empty — nothing to create." });

            MarkDuplicates(entries);

            return new BulkChartsParseResult(
                entries.Select(e => new BulkSongParseResult(e.Index, e.DisplayName,
                        e.Errors.Any() ? null : e.Song, e.Errors))
                    .ToArray(),
                Array.Empty<string>());
        }
    }

    private sealed class WorkingEntry
    {
        public int Index { get; init; }
        public string DisplayName { get; set; } = string.Empty;
        public string? RawName { get; set; }
        public BulkSongSpec? Song { get; set; }
        public List<string> Errors { get; } = new();
    }

    private static WorkingEntry ParseSong(JsonElement element, int index)
    {
        var entry = new WorkingEntry { Index = index, DisplayName = $"Song {index}" };

        RawSong? raw;
        try
        {
            raw = element.Deserialize<RawSong>(SerializerOptions);
        }
        catch (JsonException ex)
        {
            entry.Errors.Add($"Entry could not be read: {ex.Message}");
            return entry;
        }

        if (raw == null)
        {
            entry.Errors.Add("Entry must be a JSON object.");
            return entry;
        }

        if (!string.IsNullOrWhiteSpace(raw.Name))
        {
            entry.RawName = raw.Name.Trim();
            entry.DisplayName = entry.RawName;
        }

        var name = RequireName(raw.Name, "name", entry.Errors);
        var koreanName = RequireName(raw.KoreanName, "koreanName", entry.Errors,
            " (feeds the ko-KR culture mapping used by Korean-session imports)");
        var artist = RequireName(raw.Artist, "artist", entry.Errors);
        var type = ParseSongType(raw.Type, entry.Errors);
        var bpm = ParseBpm(raw.MinBpm, raw.MaxBpm, entry.Errors);
        var duration = ParseDuration(raw.DurationSeconds, entry.Errors);
        var imageUrl = ParseImageUrl(raw.ImageUrl, entry.Errors);
        var charts = ParseCharts(raw.Charts, entry.Errors);

        if (entry.Errors.Any()) return entry;

        entry.Song = new BulkSongSpec(name!.Value, koreanName!.Value, artist!.Value, type!.Value,
            bpm!.Value, duration!.Value, imageUrl!, charts);
        return entry;
    }

    private static Name? RequireName(string? value, string field, ICollection<string> errors, string hint = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"\"{field}\" is required{hint}.");
            return null;
        }

        try
        {
            return Name.From(value);
        }
        catch (Exception ex)
        {
            errors.Add($"\"{field}\" is invalid: {ex.Message}");
            return null;
        }
    }

    private static SongType? ParseSongType(string? value, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"\"type\" is required (one of: {string.Join(", ", Enum.GetNames<SongType>())}).");
            return null;
        }

        foreach (var type in Enum.GetValues<SongType>())
            if (type.ToString().Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
                return type;

        errors.Add($"\"type\" value '{value}' is not a valid song type (one of: " +
                   $"{string.Join(", ", Enum.GetNames<SongType>())}).");
        return null;
    }

    private static Bpm? ParseBpm(decimal? min, decimal? max, ICollection<string> errors)
    {
        if (min == null || max == null)
        {
            errors.Add("\"minBpm\" and \"maxBpm\" are both required.");
            return null;
        }

        try
        {
            return Bpm.From(min.Value, max.Value);
        }
        catch (Exception ex)
        {
            errors.Add($"BPM is invalid: {ex.Message}");
            return null;
        }
    }

    private static TimeSpan? ParseDuration(int? durationSeconds, ICollection<string> errors)
    {
        switch (durationSeconds)
        {
            case null:
                errors.Add("\"durationSeconds\" is required.");
                return null;
            case <= 0 or > MaxDurationSeconds:
                errors.Add($"\"durationSeconds\" must be between 1 and {MaxDurationSeconds}.");
                return null;
            default:
                return TimeSpan.FromSeconds(durationSeconds.Value);
        }
    }

    private static Uri? ParseImageUrl(string? value, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("\"imageUrl\" is required.");
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add($"\"imageUrl\" value '{value}' must be an absolute http(s) URL.");
            return null;
        }

        return url;
    }

    private static IReadOnlyList<BulkChartSpec> ParseCharts(IReadOnlyList<RawChart>? charts,
        ICollection<string> errors)
    {
        if (charts == null || !charts.Any())
        {
            errors.Add("\"charts\" must contain at least one chart.");
            return Array.Empty<BulkChartSpec>();
        }

        var results = new List<BulkChartSpec>();
        for (var i = 0; i < charts.Count; i++)
        {
            var chart = ParseChart(charts[i], $"Chart {i + 1}", errors);
            if (chart != null) results.Add(chart);
        }

        return results;
    }

    private static BulkChartSpec? ParseChart(RawChart raw, string prefix, ICollection<string> errors)
    {
        var before = errors.Count;

        ChartType? type = null;
        if (string.IsNullOrWhiteSpace(raw.Type))
        {
            errors.Add($"{prefix}: \"type\" is required (one of: " +
                       $"{string.Join(", ", AllowedChartTypes.Select(t => t.ToString()))}).");
        }
        else
        {
            foreach (var allowed in AllowedChartTypes)
                if (allowed.ToString().Equals(raw.Type.Trim(), StringComparison.OrdinalIgnoreCase))
                    type = allowed;
            if (type == null)
                errors.Add($"{prefix}: \"type\" value '{raw.Type}' is not a valid chart type (one of: " +
                           $"{string.Join(", ", AllowedChartTypes.Select(t => t.ToString()))}).");
        }

        DifficultyLevel? level = null;
        if (raw.Level == null)
            errors.Add($"{prefix}: \"level\" is required (for CoOp charts, the player count).");
        else if (!DifficultyLevel.TryParse(raw.Level.Value, out var parsedLevel))
            errors.Add($"{prefix}: \"level\" value {raw.Level} must be between {(int)DifficultyLevel.Min} " +
                       $"and {(int)DifficultyLevel.Max}.");
        else
            level = parsedLevel;

        var stepArtist = RequireChartName(raw.StepArtist, $"{prefix}: \"stepArtist\"", errors);

        string? youtubeHash = null;
        if (string.IsNullOrWhiteSpace(raw.YoutubeHash))
            errors.Add($"{prefix}: \"youtubeHash\" is required.");
        else if (!YoutubeHashRegex.IsMatch(raw.YoutubeHash.Trim()))
            errors.Add($"{prefix}: \"youtubeHash\" value '{raw.YoutubeHash}' must be only the video id " +
                       "(letters, digits, - and _), not a URL.");
        else
            youtubeHash = raw.YoutubeHash.Trim();

        var channelName = string.IsNullOrWhiteSpace(raw.ChannelName)
            ? (Name?)Name.From(DefaultChannelName)
            : RequireChartName(raw.ChannelName, $"{prefix}: \"channelName\"", errors);

        if (errors.Count > before) return null;

        return new BulkChartSpec(type!.Value, level!.Value, stepArtist!.Value, channelName!.Value,
            new Uri($"https://www.youtube.com/embed/{youtubeHash}"));
    }

    private static Name? RequireChartName(string? value, string label, ICollection<string> errors)
    {
        try
        {
            return Name.From(value ?? string.Empty);
        }
        catch (Exception ex)
        {
            errors.Add($"{label} is invalid: {ex.Message}");
            return null;
        }
    }

    private static void MarkDuplicates(IReadOnlyList<WorkingEntry> entries)
    {
        var firstSeen = new Dictionary<string, WorkingEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry.RawName == null) continue;
            if (firstSeen.TryGetValue(entry.RawName, out var first))
                entry.Errors.Add(
                    $"Duplicate song name — '{entry.RawName}' already appears as song {first.Index} in this blob.");
            else
                firstSeen.Add(entry.RawName, entry);
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }

        value = default;
        return false;
    }

    private sealed class RawSong
    {
        public string? Name { get; set; }
        public string? KoreanName { get; set; }
        public string? Artist { get; set; }
        public string? Type { get; set; }
        public decimal? MinBpm { get; set; }
        public decimal? MaxBpm { get; set; }
        public int? DurationSeconds { get; set; }
        public string? ImageUrl { get; set; }
        public List<RawChart>? Charts { get; set; }
    }

    private sealed class RawChart
    {
        public string? Type { get; set; }
        public int? Level { get; set; }
        public string? StepArtist { get; set; }
        public string? YoutubeHash { get; set; }
        public string? ChannelName { get; set; }
    }
}
