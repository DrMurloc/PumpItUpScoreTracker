using System.Globalization;
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

            // Root is [taps, holds, metadata]: taps are [pad, time, limb], holds
            // [pad, start, end, limb], and the metadata object is the last element.
            JsonElement meta = default;
            var found = false;
            var noteArrays = new List<JsonElement>(2);
            foreach (var element in document.RootElement.EnumerateArray())
                if (element.ValueKind == JsonValueKind.Object)
                {
                    meta = element;
                    found = true;
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    noteArrays.Add(element);
                }

            if (!found) return null;

            // Rows, not arrows: the game judges once per row, so a jump is one judgement — and
            // a rolling bracket, written one arrow per finely-offset row, is one per arrow.
            var tapRows = CountDistinctStartTimes(noteArrays.Count > 0 ? noteArrays[0] : default);
            var holdRows = CountDistinctStartTimes(noteArrays.Count > 1 ? noteArrays[1] : default);

            // Their per-hold tick tally, summed. Pre-Phoenix hold data — banked for
            // diagnostics only, never shown (design doc D13).
            var holdTickSum = 0;
            if (meta.TryGetProperty("Hold ticks", out var holdTicks) &&
                holdTicks.ValueKind == JsonValueKind.Array)
                foreach (var tick in holdTicks.EnumerateArray())
                    if (tick.ValueKind == JsonValueKind.Array && tick.GetArrayLength() >= 3 &&
                        tick[2].ValueKind == JsonValueKind.Number)
                        holdTickSum += (int)tick[2].GetDecimal();

            var segmentCount = 0;
            var badgeCounts = new Dictionary<string, int>();
            var rareCounts = new Dictionary<string, int>();
            IReadOnlyList<string> lastSegmentSkills = Array.Empty<string>();
            decimal lastSegmentLevel = 0;
            decimal maxSegmentLevel = 0;
            // Crux candidate: the FIRST segment reaching the maximum level, so a chart whose
            // closing section ties its own peak is credited to the earlier one (the peak is
            // where the chart first gets that hard).
            var cruxIndex = -1;
            IReadOnlyList<string> cruxBadges = Array.Empty<string>();
            decimal? cruxEnps = null;
            if (meta.TryGetProperty("Segment metadata", out var segments) &&
                segments.ValueKind == JsonValueKind.Array)
                foreach (var segment in segments.EnumerateArray())
                {
                    var badges = ReadStringArray(segment, "Skill badges");
                    lastSegmentSkills = badges;
                    lastSegmentLevel = ReadDecimal(segment, "level") ?? 0;
                    if (segmentCount == 0 || lastSegmentLevel > maxSegmentLevel)
                    {
                        cruxIndex = segmentCount;
                        cruxBadges = badges;
                        cruxEnps = ReadDecimal(segment, "eNPS");
                        maxSegmentLevel = lastSegmentLevel;
                    }

                    segmentCount++;
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
                meta.TryGetProperty("sord_chartlevel", out var sord) ? sord.GetString() : null,
                tapRows,
                holdRows,
                holdTickSum,
                ReadCrux(meta, cruxIndex, maxSegmentLevel, cruxEnps, cruxBadges),
                StanceAnalyzer.Analyze(ReadArrows(noteArrays)),
                meta.TryGetProperty("pack", out var pack) ? pack.GetString() : null,
                ReadChartSpan(meta));
        }

        /// <summary>
        ///     The same chart page read raw (docs/design/step-chart-failure-map.md D4): every
        ///     arrow, hold, authored tick tally and segment span, where <see cref="ParseChartPage" />
        ///     keeps only aggregates. A separate method rather than a flag so the aggregate path's
        ///     shape — and every caller banking metrics off it — cannot drift while the step-chart
        ///     ingest evolves.
        /// </summary>
        public static PiuCenterChartSteps? ParseChartPageSteps(string json)
        {
            if (!LooksLikeJson(json)) return null;
            using var document = JsonDocument.Parse(json);

            JsonElement meta = default;
            var found = false;
            var noteArrays = new List<JsonElement>(2);
            foreach (var element in document.RootElement.EnumerateArray())
                if (element.ValueKind == JsonValueKind.Object)
                {
                    meta = element;
                    found = true;
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    noteArrays.Add(element);
                }

            if (!found) return null;

            var taps = new List<StepArrow>();
            if (noteArrays.Count > 0 && noteArrays[0].ValueKind == JsonValueKind.Array)
                foreach (var note in noteArrays[0].EnumerateArray())
                    if (note.ValueKind == JsonValueKind.Array && note.GetArrayLength() >= 3 &&
                        note[0].ValueKind == JsonValueKind.Number && note[1].ValueKind == JsonValueKind.Number &&
                        note[2].ValueKind == JsonValueKind.String)
                        taps.Add(new StepArrow((int)note[0].GetDecimal(), note[1].GetDecimal(),
                            note[2].GetString() ?? string.Empty));

            var holds = new List<PiuCenterStepHold>();
            if (noteArrays.Count > 1 && noteArrays[1].ValueKind == JsonValueKind.Array)
                foreach (var note in noteArrays[1].EnumerateArray())
                    if (note.ValueKind == JsonValueKind.Array && note.GetArrayLength() >= 4 &&
                        note[0].ValueKind == JsonValueKind.Number && note[1].ValueKind == JsonValueKind.Number &&
                        note[2].ValueKind == JsonValueKind.Number && note[3].ValueKind == JsonValueKind.String)
                        holds.Add(new PiuCenterStepHold((int)note[0].GetDecimal(), note[1].GetDecimal(),
                            note[2].GetDecimal(), note[3].GetString() ?? string.Empty));

            var tickSpans = new List<PiuCenterTickSpan>();
            if (meta.TryGetProperty("Hold ticks", out var holdTicks) &&
                holdTicks.ValueKind == JsonValueKind.Array)
                foreach (var tick in holdTicks.EnumerateArray())
                    if (tick.ValueKind == JsonValueKind.Array && tick.GetArrayLength() >= 3 &&
                        tick[0].ValueKind == JsonValueKind.Number && tick[1].ValueKind == JsonValueKind.Number &&
                        tick[2].ValueKind == JsonValueKind.Number)
                        tickSpans.Add(new PiuCenterTickSpan(tick[0].GetDecimal(), tick[1].GetDecimal(),
                            (int)tick[2].GetDecimal()));

            var segments = new List<PiuCenterSegmentSpan>();
            if (meta.TryGetProperty("Segments", out var spans) && spans.ValueKind == JsonValueKind.Array)
            {
                var haveMetadata = meta.TryGetProperty("Segment metadata", out var segmentMetadata) &&
                                   segmentMetadata.ValueKind == JsonValueKind.Array;
                var index = 0;
                foreach (var span in spans.EnumerateArray())
                {
                    if (span.ValueKind == JsonValueKind.Array && span.GetArrayLength() >= 2 &&
                        span[0].ValueKind == JsonValueKind.Number && span[1].ValueKind == JsonValueKind.Number)
                    {
                        var withMeta = haveMetadata && index < segmentMetadata.GetArrayLength();
                        segments.Add(new PiuCenterSegmentSpan(span[0].GetDecimal(), span[1].GetDecimal(),
                            withMeta ? ReadDecimal(segmentMetadata[index], "eNPS") : null,
                            withMeta ? ReadStringArray(segmentMetadata[index], "Skill badges") : null,
                            withMeta ? ReadDecimal(segmentMetadata[index], "level") : null));
                    }

                    index++;
                }
            }

            var ranges = new List<PiuCenterRangeSpan>();
            if (meta.TryGetProperty("eNPS ranges of interest", out var interest) &&
                interest.ValueKind == JsonValueKind.Array)
                foreach (var range in interest.EnumerateArray())
                    if (range.ValueKind == JsonValueKind.Array && range.GetArrayLength() >= 2 &&
                        range[0].ValueKind == JsonValueKind.Number && range[1].ValueKind == JsonValueKind.Number)
                        ranges.Add(new PiuCenterRangeSpan(range[0].GetDecimal(), range[1].GetDecimal()));

            return new PiuCenterChartSteps(
                taps, holds, tickSpans, segments, ranges,
                meta.TryGetProperty("ssc_file", out var ssc) && ssc.ValueKind == JsonValueKind.String
                    ? ssc.GetString()
                    : null,
                meta.TryGetProperty("STEPSTYPE", out var steps) && steps.ValueKind == JsonValueKind.String
                    ? steps.GetString()
                    : null,
                meta.TryGetProperty("METER", out var meter) &&
                int.TryParse(meter.ValueKind == JsonValueKind.String ? meter.GetString() : meter.ToString(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out var printed)
                    ? printed
                    : null);
        }

        /// <summary>
        ///     The played span, first segment start to last segment end. Their own "Sustain time"
        ///     is the longest single run in seconds, and seconds alone cannot say whether a run is
        ///     the chart or an incident in it — a fifty-second run is most of a short chart and a
        ///     quarter of Baroque Virus.
        /// </summary>
        private static decimal ReadChartSpan(JsonElement meta)
        {
            if (!meta.TryGetProperty("Segments", out var spans) || spans.ValueKind != JsonValueKind.Array)
                return 0;
            var count = spans.GetArrayLength();
            if (count == 0) return 0;
            var first = spans[0];
            var last = spans[count - 1];
            if (first.ValueKind != JsonValueKind.Array || first.GetArrayLength() < 2) return 0;
            if (last.ValueKind != JsonValueKind.Array || last.GetArrayLength() < 2) return 0;
            return Math.Round(last[1].GetDecimal() - first[0].GetDecimal(), 4);
        }

        /// <summary>
        ///     Every arrow with the foot piucenter assigns it, taps and hold heads alike
        ///     (docs/design/chart-identity.md §4b). A hold counts once, where it starts: holding
        ///     the centre for ten seconds is one commitment to that panel, not ten seconds of
        ///     evidence about where the player stands.
        /// </summary>
        private static IReadOnlyList<StepArrow> ReadArrows(IReadOnlyList<JsonElement> noteArrays)
        {
            var arrows = new List<StepArrow>();
            for (var i = 0; i < noteArrays.Count && i < 2; i++)
            {
                if (noteArrays[i].ValueKind != JsonValueKind.Array) continue;
                foreach (var note in noteArrays[i].EnumerateArray())
                {
                    if (note.ValueKind != JsonValueKind.Array) continue;
                    var length = note.GetArrayLength();
                    // Taps are [panel, time, limb]; holds are [panel, start, end, limb], so the
                    // foot is always the last element and the start time always the second.
                    if (length < 3 || note[0].ValueKind != JsonValueKind.Number ||
                        note[1].ValueKind != JsonValueKind.Number) continue;
                    var limb = note[length - 1].ValueKind == JsonValueKind.String
                        ? note[length - 1].GetString()
                        : null;
                    if (string.IsNullOrEmpty(limb)) continue;
                    arrows.Add(new StepArrow((int)note[0].GetDecimal(), note[1].GetDecimal(), limb));
                }
            }

            return arrows;
        }

        /// <summary>
        ///     Places the crux inside the chart: its position across the played span, how long it
        ///     lasts, and how far its level runs over the level the game prints
        ///     (docs/design/chart-identity.md §4). Returns null when the page carries no segments,
        ///     or when its Segments and Segment metadata arrays disagree in length — that pairing
        ///     is the whole basis of the reading, and a mismatch means we cannot say which span
        ///     the crux occupies.
        /// </summary>
        private static PiuCenterCrux? ReadCrux(JsonElement meta, int cruxIndex, decimal cruxLevel,
            decimal? cruxEnps, IReadOnlyList<string> cruxBadges)
        {
            if (cruxIndex < 0) return null;
            if (!meta.TryGetProperty("Segments", out var spans) || spans.ValueKind != JsonValueKind.Array) return null;
            var spanCount = spans.GetArrayLength();
            if (cruxIndex >= spanCount) return null;

            // [start, end, startNote, endNote] per segment, seconds for the first two.
            var crux = spans[cruxIndex];
            if (crux.ValueKind != JsonValueKind.Array || crux.GetArrayLength() < 2) return null;
            var start = crux[0].GetDecimal();
            var end = crux[1].GetDecimal();
            var chartStart = spans[0][0].GetDecimal();
            var chartEnd = spans[spanCount - 1][1].GetDecimal();
            var span = chartEnd - chartStart;

            return new PiuCenterCrux(
                cruxLevel,
                // METER is written as a string ("20"), and a page without one still has a
                // readable crux — only the against-the-printed-level reading is unavailable.
                meta.TryGetProperty("METER", out var meter) &&
                decimal.TryParse(meter.ValueKind == JsonValueKind.String ? meter.GetString() : meter.ToString(),
                    NumberStyles.Number, CultureInfo.InvariantCulture, out var printed)
                    ? Math.Round(cruxLevel - printed, 4)
                    : null,
                span > 0 ? Math.Round((start - chartStart) / span, 4) : 0,
                Math.Round(end - start, 4),
                cruxEnps,
                cruxBadges);
        }

        /// <summary>
        ///     Distinct start times in a note array — element 1 of each entry, for taps and holds
        ///     alike. Times compare as their exact decimal text, which is how the generator writes
        ///     coincident notes; a jump's arrows share one time, a roll's arrows each carry their
        ///     own.
        /// </summary>
        private static int CountDistinctStartTimes(JsonElement noteArray)
        {
            if (noteArray.ValueKind != JsonValueKind.Array) return 0;
            var times = new HashSet<decimal>();
            foreach (var note in noteArray.EnumerateArray())
                if (note.ValueKind == JsonValueKind.Array && note.GetArrayLength() >= 2 &&
                    note[1].ValueKind == JsonValueKind.Number)
                    times.Add(note[1].GetDecimal());
            return times.Count;
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
