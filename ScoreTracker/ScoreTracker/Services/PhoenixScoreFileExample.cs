using System.Dynamic;
using System.Globalization;
using Microsoft.Extensions.Localization;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The example spreadsheet the import page hands out: the five columns
///     <see cref="PhoenixScoreFileExtractor" /> reads plus a Notes column it skips, over five rows written in
///     the selected mix's own terms. Every row is a chart that mix has, so the file uploads as it is.
/// </summary>
public static class PhoenixScoreFileExample
{
    private const string True = "TRUE";
    private const string False = "FALSE";

    // On a mix that hands out the eight plates, every pass carries one.
    private static readonly ExampleRow[] PlateRows =
    {
        new("BRAIN POWER", false, 16, PhoenixPlate.UltimateGame, 993512, false),
        new("Conflict", false, 17, PhoenixPlate.SuperbGame, 978220, false),
        new("Chinese restaurant", false, 18, PhoenixPlate.MarvelousGame, 951004, false),
        new("Final Audition 2", true, 20, PhoenixPlate.FairGame, 902750, false),
        new("Switronic", true, 22, null, 734560, true)
    };

    // RISE's marks start at No Miss, so a clear with a miss leaves its Plate empty.
    private static readonly ExampleRow[] MarkRows =
    {
        new("BRAIN POWER", false, 16, PhoenixPlate.PerfectGame, 1000000, false),
        new("Conflict", false, 17, PhoenixPlate.UltimateGame, 991250, false),
        new("Chinese restaurant", false, 18, PhoenixPlate.SuperbGame, 968400, false),
        new("Final Audition 2", true, 20, null, 921700, false),
        new("Switronic", true, 18, null, 702300, true)
    };

    public static string FileName(MixEnum mix)
    {
        return $"piu-scores-example-{mix.GetName().ToLowerInvariant().Replace(' ', '-')}.csv";
    }

    /// <summary>
    ///     The example's rows, keyed by their column headers. Each row takes its song's chart of the row's type
    ///     closest to the row's level in <paramref name="charts" />, else the closest-level chart of that type,
    ///     and never a chart an earlier row took; a row whose type the catalog has none of is left out.
    ///     <para>
    ///         Dynamic because the Notes header is translated. Typed as <c>object</c> so CSV writing reads the
    ///         headers off the first row rather than off a record type.
    ///     </para>
    /// </summary>
    public static IReadOnlyList<dynamic> Rows(MixEnum mix, IEnumerable<Chart> charts, IStringLocalizer localizer)
    {
        var profile = MixProfiles.For(mix);
        var twoPadType = profile.ChartTypes.Contains(ChartType.Double) ? ChartType.Double : ChartType.HalfDouble;
        var (rows, notes) = profile.Awards switch
        {
            AwardSet.PhoenixPlates => (PlateRows, PlateNotes(localizer, twoPadType)),
            AwardSet.RiseMarks => (MarkRows, MarkNotes(localizer)),
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix, "This mix uploads through the XX page")
        };
        var notesHeader = localizer["Notes (not imported)"].Value;
        var catalog = charts.ToArray();
        var taken = new HashSet<Guid>();
        var result = new List<dynamic>();
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var type = row.TwoPad ? twoPadType : ChartType.Single;
            var open = catalog.Where(c => c.Type == type && !taken.Contains(c.Id)).ToArray();
            var chart = Closest(open.Where(c => c.Song.Name == row.Song), row.Level) ?? Closest(open, row.Level);
            if (chart == null) continue;
            taken.Add(chart.Id);

            IDictionary<string, object?> cells = new ExpandoObject();
            cells[nameof(PhoenixSpreadsheetScoreDto.Song)] = chart.Song.Name.ToString();
            cells[nameof(PhoenixSpreadsheetScoreDto.Difficulty)] = chart.DifficultyString;
            cells[nameof(PhoenixSpreadsheetScoreDto.Score)] = row.Score.ToString(CultureInfo.InvariantCulture);
            cells[nameof(PhoenixSpreadsheetScoreDto.Plate)] = row.Plate?.GetShorthand(mix) ?? "";
            cells[nameof(PhoenixSpreadsheetScoreDto.IsBroken)] = row.IsBroken ? True : False;
            cells[notesHeader] = notes[i];
            result.Add(cells);
        }

        return result;
    }

    private static Chart? Closest(IEnumerable<Chart> charts, int level)
    {
        return charts.OrderBy(c => Math.Abs((int)c.Level - level))
            .ThenBy(c => (int)c.Level)
            .ThenBy(c => c.Song.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id)
            .FirstOrDefault();
    }

    // The column names, chart-type codes and cell values ride in as arguments, so no translation can change
    // what the file says.
    private static string[] PlateNotes(IStringLocalizer localizer, ChartType twoPadType)
    {
        return new[]
        {
            SongNote(localizer),
            ScoreNote(localizer),
            localizer["{0}: PG UG EG SG MG TG FG RG, Perfect Game down to Rough Game.",
                nameof(PhoenixSpreadsheetScoreDto.Plate)].Value,
            localizer["{0}: {1} or {2}, then the level.", nameof(PhoenixSpreadsheetScoreDto.Difficulty),
                ChartType.Single.GetShortHand(), twoPadType.GetShortHand()].Value,
            StageBreakNote(localizer)
        };
    }

    private static string[] MarkNotes(IStringLocalizer localizer)
    {
        return new[]
        {
            SongNote(localizer),
            ScoreNote(localizer),
            localizer["{0}: PG Perfect Game · FC Full Combo · NM No Miss",
                nameof(PhoenixSpreadsheetScoreDto.Plate)].Value,
            localizer["{0} is half-double. Cleared with no mark: leave {1} empty.",
                ChartType.HalfDouble.GetShortHand(), nameof(PhoenixSpreadsheetScoreDto.Plate)].Value,
            StageBreakNote(localizer)
        };
    }

    private static string SongNote(IStringLocalizer localizer)
    {
        return localizer["{0}: as the game spells it. Capitals don't matter.",
            nameof(PhoenixSpreadsheetScoreDto.Song)].Value;
    }

    private static string ScoreNote(IStringLocalizer localizer)
    {
        return localizer["{0}: digits only, no commas.", nameof(PhoenixSpreadsheetScoreDto.Score)].Value;
    }

    private static string StageBreakNote(IStringLocalizer localizer)
    {
        return localizer["Stage break: {0} {1}, {2} empty.", nameof(PhoenixSpreadsheetScoreDto.IsBroken), True,
            nameof(PhoenixSpreadsheetScoreDto.Plate)].Value;
    }

    // TwoPad: played on both pads, which is Double, or the half-double on a mix that has no Doubles.
    private sealed record ExampleRow(Name Song, bool TwoPad, int Level, PhoenixPlate? Plate, int Score, bool IsBroken);
}
