using System.Text.Json;
using System.Text.RegularExpressions;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.Apis
{
    /// <summary>
    ///     piucenter's JSON shapes parsed to domain records — shared by the HTTP client
    ///     and the admin snapshot import (a zipped copy of the same files), so the two
    ///     ingestion paths can never drift. Every method takes the raw body and returns
    ///     null/empty on non-JSON input (their static host serves the SPA shell for
    ///     unknown files).
    /// </summary>
    public static class PiuCenterDataParser
    {
        private static readonly Regex KeyVariantPattern =
            new("_((?:HALFDOUBLE_)?(?:ARCADE|REMIX|SHORTCUT|FULLSONG))$", RegexOptions.Compiled);

        public static bool LooksLikeJson(string body)
        {
            var trimmed = body.TrimStart();
            return trimmed.StartsWith('[') || trimmed.StartsWith('{');
        }

        public static IReadOnlyList<PiuCenterChartListing> ParseChartTable(string json)
        {
            if (!LooksLikeJson(json)) return Array.Empty<PiuCenterChartListing>();
            using var document = JsonDocument.Parse(json);
            var listings = new List<PiuCenterChartListing>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var key = row.GetProperty("name").GetString() ?? string.Empty;
                var type = row.GetProperty("sord").GetString() switch
                {
                    "singles" => ChartType.Single,
                    "doubles" => ChartType.Double,
                    _ => (ChartType?)null
                };
                if (key.Length == 0 || type == null) continue;

                var variantMatch = KeyVariantPattern.Match(key);
                listings.Add(new PiuCenterChartListing(
                    key,
                    type.Value,
                    row.GetProperty("level").GetInt32(),
                    row.TryGetProperty("pack", out var pack) ? pack.GetString() ?? string.Empty : string.Empty,
                    variantMatch.Success ? variantMatch.Groups[1].Value : string.Empty,
                    ReadStringArray(row, "skills"),
                    ReadDecimal(row, "NPS") ?? 0,
                    row.TryGetProperty("BPM info", out var bpm) ? bpm.GetString() ?? string.Empty : string.Empty,
                    ReadDecimal(row, "Sustain time") ?? 0,
                    ReadDecimal(row, "Total time under tension") ?? 0));
            }

            return listings;
        }

        public static PiuCenterChartPage? ParseChartPage(string externalKey, string json)
        {
            if (!LooksLikeJson(json)) return null;
            using var document = JsonDocument.Parse(json);

            // Root is [notes, holds, metadata]; the metadata object is the last element.
            JsonElement meta = default;
            var found = false;
            foreach (var element in document.RootElement.EnumerateArray())
                if (element.ValueKind == JsonValueKind.Object)
                {
                    meta = element;
                    found = true;
                }

            if (!found) return null;

            var segmentCount = 0;
            var badgeCounts = new Dictionary<string, int>();
            var rareCounts = new Dictionary<string, int>();
            IReadOnlyList<string> lastSegmentSkills = Array.Empty<string>();
            decimal lastSegmentLevel = 0;
            decimal maxSegmentLevel = 0;
            if (meta.TryGetProperty("Segment metadata", out var segments) &&
                segments.ValueKind == JsonValueKind.Array)
                foreach (var segment in segments.EnumerateArray())
                {
                    segmentCount++;
                    var badges = ReadStringArray(segment, "Skill badges");
                    lastSegmentSkills = badges;
                    lastSegmentLevel = ReadDecimal(segment, "level") ?? 0;
                    if (lastSegmentLevel > maxSegmentLevel) maxSegmentLevel = lastSegmentLevel;
                    foreach (var badge in badges)
                        badgeCounts[badge] = badgeCounts.TryGetValue(badge, out var count) ? count + 1 : 1;
                    foreach (var rare in ReadStringArray(segment, "rare skills"))
                        rareCounts[rare] = rareCounts.TryGetValue(rare, out var count) ? count + 1 : 1;
                }

            return new PiuCenterChartPage(
                externalKey,
                ReadStringArray(meta, "chart_skill_summary"),
                segmentCount,
                badgeCounts,
                rareCounts,
                lastSegmentSkills,
                segmentCount > 0 && lastSegmentLevel >= maxSegmentLevel,
                ReadDecimal(meta, "nps_summary"),
                meta.TryGetProperty("notetype_bpm_summary", out var notetype) ? notetype.GetString() : null,
                meta.TryGetProperty("sord_chartlevel", out var sord) ? sord.GetString() : null);
        }

        public static IReadOnlyList<PiuCenterPracticeEntry> ParsePracticeLists(string json)
        {
            if (!LooksLikeJson(json)) return Array.Empty<PiuCenterPracticeEntry>();
            using var document = JsonDocument.Parse(json);

            // Root is [lists, descriptions]; element 0 maps skill -> sord-level -> ranked keys.
            var entries = new List<PiuCenterPracticeEntry>();
            var lists = document.RootElement[0];
            foreach (var skill in lists.EnumerateObject())
            foreach (var level in skill.Value.EnumerateObject())
            {
                var rank = 0;
                foreach (var key in level.Value.EnumerateArray())
                {
                    rank++;
                    var externalKey = key.GetString();
                    if (externalKey != null)
                        entries.Add(new PiuCenterPracticeEntry(skill.Name, level.Name, rank, externalKey));
                }
            }

            return entries;
        }

        public static IReadOnlyDictionary<string, decimal> ParseDifficultyPredictions(string json)
        {
            var predictions = new Dictionary<string, decimal>();
            if (!LooksLikeJson(json)) return predictions;
            using var document = JsonDocument.Parse(json);

            // Folder -> NPS-cluster label -> [keys[], predictions[]]; flattened, a key
            // only appears in its own folder.
            foreach (var folder in document.RootElement.EnumerateObject())
            foreach (var cluster in folder.Value.EnumerateObject())
            {
                if (cluster.Value.ValueKind != JsonValueKind.Array || cluster.Value.GetArrayLength() < 2) continue;
                var keys = cluster.Value[0];
                var values = cluster.Value[1];
                var count = Math.Min(keys.GetArrayLength(), values.GetArrayLength());
                for (var i = 0; i < count; i++)
                {
                    var key = keys[i].GetString();
                    if (key != null && values[i].ValueKind == JsonValueKind.Number)
                        predictions[key] = values[i].GetDecimal();
                }
            }

            return predictions;
        }

        private static IReadOnlyList<string> ReadStringArray(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();
            return array.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToArray();
        }

        private static decimal? ReadDecimal(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDecimal()
                : null;
        }
    }
}
