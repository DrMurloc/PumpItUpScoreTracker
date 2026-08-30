namespace ScoreTracker.Web.Services;

/// <summary>
///     What the player asked the downloaded image to carry
///     (docs/design/share-card-download-settings.md). One remembered setting serves both
///     Download buttons — the tier list's and the PUMBILITY Targets' — so the two pictures
///     speak one language.
///     <para>
///         Sub-choices are stored even while their parent is off (turning Scores back on
///         remembers whether broken runs printed), so nothing here normalizes them away; a
///         flag is simply inert without its parent. The UI enforces the exclusive trio
///         (the two color modes and Top 50) at press time; the composer resolves any stored
///         overlap by ladder order rather than throwing.
///     </para>
/// </summary>
public sealed record ShareCardOptions
{
    /// <summary>The UiSettings key both surfaces read (design doc §1).</summary>
    public const string SettingKey = "ShareCard__Options";

    /// <summary>
    ///     Distinguishes "saved with everything off" from "never saved": an empty flag list
    ///     still carries the version token, where a missing setting parses to the defaults.
    /// </summary>
    private const string Version = "v1";

    public bool SongNames { get; init; }
    public bool LetterGrades { get; init; } = true;
    public bool Plates { get; init; } = true;
    public bool Scores { get; init; }
    public bool IncludeBrokenScores { get; init; } = true;
    public bool Pumbility { get; init; }
    public bool ExpectedGains { get; init; }
    public bool Skills { get; init; }
    public bool BoundaryTodo { get; init; } = true;
    public bool BoundaryPass { get; init; } = true;
    public bool ColorByLetterGrade { get; init; }
    public bool ColorByPlate { get; init; }
    public bool BoundaryOtherMixes { get; init; }
    public bool BoundaryBroken { get; init; } = true;
    public bool BoundaryTop50 { get; init; }

    /// <summary>Today-parity: the picture a player gets before they have ever opened the dialog.</summary>
    public static ShareCardOptions Default => new();

    private static ShareCardOptions AllOff => new()
    {
        LetterGrades = false, Plates = false, IncludeBrokenScores = false,
        BoundaryTodo = false, BoundaryPass = false, BoundaryBroken = false
    };

    public string Serialize()
    {
        var flags = new List<string> { Version };
        foreach (var (name, value) in Flags())
            if (value)
                flags.Add(name);
        return string.Join(',', flags);
    }

    /// <summary>Unknown tokens are ignored, so a rolled-back release can read a newer save.</summary>
    public static ShareCardOptions Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return Default;
        var tokens = stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!tokens.Contains(Version)) return Default;
        return AllOff with
        {
            SongNames = tokens.Contains(nameof(SongNames)),
            LetterGrades = tokens.Contains(nameof(LetterGrades)),
            Plates = tokens.Contains(nameof(Plates)),
            Scores = tokens.Contains(nameof(Scores)),
            IncludeBrokenScores = tokens.Contains(nameof(IncludeBrokenScores)),
            Pumbility = tokens.Contains(nameof(Pumbility)),
            ExpectedGains = tokens.Contains(nameof(ExpectedGains)),
            Skills = tokens.Contains(nameof(Skills)),
            BoundaryTodo = tokens.Contains(nameof(BoundaryTodo)),
            BoundaryPass = tokens.Contains(nameof(BoundaryPass)),
            ColorByLetterGrade = tokens.Contains(nameof(ColorByLetterGrade)),
            ColorByPlate = tokens.Contains(nameof(ColorByPlate)),
            BoundaryOtherMixes = tokens.Contains(nameof(BoundaryOtherMixes)),
            BoundaryBroken = tokens.Contains(nameof(BoundaryBroken)),
            BoundaryTop50 = tokens.Contains(nameof(BoundaryTop50))
        };
    }

    private IEnumerable<(string Name, bool Value)> Flags()
    {
        yield return (nameof(SongNames), SongNames);
        yield return (nameof(LetterGrades), LetterGrades);
        yield return (nameof(Plates), Plates);
        yield return (nameof(Scores), Scores);
        yield return (nameof(IncludeBrokenScores), IncludeBrokenScores);
        yield return (nameof(Pumbility), Pumbility);
        yield return (nameof(ExpectedGains), ExpectedGains);
        yield return (nameof(Skills), Skills);
        yield return (nameof(BoundaryTodo), BoundaryTodo);
        yield return (nameof(BoundaryPass), BoundaryPass);
        yield return (nameof(ColorByLetterGrade), ColorByLetterGrade);
        yield return (nameof(ColorByPlate), ColorByPlate);
        yield return (nameof(BoundaryOtherMixes), BoundaryOtherMixes);
        yield return (nameof(BoundaryBroken), BoundaryBroken);
        yield return (nameof(BoundaryTop50), BoundaryTop50);
    }
}
