using System.Text;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Reads a StepMania .ssc file into its tag structure: the song-level header and one block
///     per chart (each starting at <c>#NOTEDATA:;</c>), with chart-level timing tags kept
///     separate so they can override the song's (docs/design/step-chart-failure-map.md D4).
///     Text in, records out — no I/O, no ports; the timeline math lives in
///     <see cref="StepChartTimeline" />.
/// </summary>
internal static class StepFileParser
{
    /// <summary>
    ///     Tags, in order, split into the song header and the chart blocks. A value runs from
    ///     the first ':' to the terminating ';' and may span lines (note data always does);
    ///     '//' comments are stripped to end of line first, which is also how StepMania reads
    ///     the format.
    /// </summary>
    public static StepFileDocument Parse(string text)
    {
        var songTags = new List<KeyValuePair<string, string>>();
        var charts = new List<List<KeyValuePair<string, string>>>();
        List<KeyValuePair<string, string>>? current = null;

        foreach (var (name, value) in ScanTags(text))
            if (name.Equals("NOTEDATA", StringComparison.OrdinalIgnoreCase))
            {
                current = new List<KeyValuePair<string, string>>();
                charts.Add(current);
            }
            else
            {
                (current ?? songTags).Add(new KeyValuePair<string, string>(name.ToUpperInvariant(), value));
            }

        return new StepFileDocument(
            new StepTagBag(songTags),
            charts.Select(c => new StepChartBlock(new StepTagBag(c))).ToArray());
    }

    /// <summary>
    ///     The chart a snapshot entry names: same steps type, same printed meter. Ties fall to
    ///     the first block — the alignment tripwire downstream is what catches a wrong pick,
    ///     the same way it catches every other disagreement (D6).
    /// </summary>
    public static StepChartBlock? SelectChart(StepFileDocument document, string stepsType, int meter)
    {
        return document.Charts.FirstOrDefault(c =>
            string.Equals(c.Tags.Get("STEPSTYPE"), stepsType, StringComparison.OrdinalIgnoreCase) &&
            c.Meter == meter);
    }

    private static IEnumerable<(string Name, string Value)> ScanTags(string text)
    {
        var stripped = StripComments(text);
        var i = 0;
        while (i < stripped.Length)
        {
            var hash = stripped.IndexOf('#', i);
            if (hash < 0) yield break;
            var end = stripped.IndexOf(';', hash + 1);
            if (end < 0) end = stripped.Length;
            var body = stripped.Substring(hash + 1, end - hash - 1);
            var colon = body.IndexOf(':');
            if (colon >= 0)
                yield return (body[..colon].Trim(), body[(colon + 1)..].Trim());
            i = end + 1;
        }
    }

    private static string StripComments(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) line = line[..comment];
            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }
}

internal sealed record StepFileDocument(StepTagBag SongTags, IReadOnlyList<StepChartBlock> Charts);

internal sealed record StepChartBlock(StepTagBag Tags)
{
    public int Meter => int.TryParse(Tags.Get("METER"), out var meter) ? meter : 0;
    public string StepsType => Tags.Get("STEPSTYPE") ?? string.Empty;
}

/// <summary>Ordered tag list with last-wins lookup, the way StepMania resolves duplicates.</summary>
internal sealed class StepTagBag
{
    private readonly IReadOnlyList<KeyValuePair<string, string>> _tags;

    public StepTagBag(IReadOnlyList<KeyValuePair<string, string>> tags)
    {
        _tags = tags;
    }

    public string? Get(string name)
    {
        for (var i = _tags.Count - 1; i >= 0; i--)
            if (_tags[i].Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                return _tags[i].Value;
        return null;
    }
}
