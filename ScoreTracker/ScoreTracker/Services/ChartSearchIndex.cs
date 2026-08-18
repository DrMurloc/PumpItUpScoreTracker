using System.Text.RegularExpressions;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The chart-name search every chart picker runs: a catalog indexed by its display name
///     ("Song S22"), a term that may end in a difficulty shorthand which narrows to that type and
///     level, and an order that puts an exact song name first, then type, then level. One
///     implementation for the pickers and the site search, so "what does typing 'gargoyle d22'
///     find" has one answer.
/// </summary>
public sealed class ChartSearchIndex
{
    // A trailing " S22" / " D18" / " C2" is a difficulty, not part of the song name.
    private static readonly Regex DifficultyRegex =
        new(@"\s(S|D|C)[0-9]{1,2}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IReadOnlyDictionary<string, Chart> _byName;

    private ChartSearchIndex(IReadOnlyDictionary<string, Chart> byName)
    {
        _byName = byName;
    }

    public static ChartSearchIndex Empty { get; } =
        new(new Dictionary<string, Chart>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    ///     DifficultyDisplay, not DifficultyString: pre-Exceed slots make "S6" ambiguous (Hard 6 vs
    ///     Crazy 6). Duplicate display names collapse to the first — a data quirk must not take
    ///     down a circuit.
    /// </summary>
    public static ChartSearchIndex Build(IEnumerable<Chart> charts)
    {
        var byName = charts
            .GroupBy(NameOf, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return new ChartSearchIndex(byName);
    }

    public static string NameOf(Chart chart) => chart.Song.Name + " " + chart.DifficultyDisplay;

    /// <summary>Every indexed display name → its chart. What a picker's item template renders from.</summary>
    public IReadOnlyDictionary<string, Chart> ByName => _byName;

    public bool TryGet(string name, out Chart chart) => _byName.TryGetValue(name, out chart!);

    /// <summary>The matching display names, best first. An empty term is the whole catalog, alphabetical.</summary>
    public IEnumerable<string> SearchNames(string term) => Matches(term).Select(kv => kv.Key);

    /// <summary>The matching charts, best first.</summary>
    public IEnumerable<Chart> Search(string term) => Matches(term).Select(kv => kv.Value);

    private IEnumerable<KeyValuePair<string, Chart>> Matches(string term)
    {
        term ??= string.Empty;
        ChartType? type = null;
        DifficultyLevel? level = null;
        var difficultyMatch = DifficultyRegex.Match(term);
        if (difficultyMatch.Success &&
            DifficultyLevel.TryParseShortHand(difficultyMatch.Value.Trim(), out var parsedType, out var parsedLevel))
        {
            type = parsedType;
            level = parsedLevel;
            term = term[..difficultyMatch.Index];
        }

        var text = term.Trim();
        return _byName
            .Where(c => type == null || level == null || (c.Value.Type == type && c.Value.Level == level))
            .Where(c => text.Length == 0 || c.Key.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => text.Length == 0
                ? c.Value.Song.Name.ToString()
                : c.Value.Song.Name == text
                    ? "a" + c.Value.Song.Name
                    : "b" + c.Value.Song.Name)
            .ThenBy(c => c.Value.Type)
            .ThenBy(c => c.Value.Level);
    }
}
