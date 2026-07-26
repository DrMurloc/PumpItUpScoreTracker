using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The payload behind a <see cref="MilestoneKind.FolderProgress" /> milestone, packed into the
///     milestone's Detail column. One kind carries every folder movement, and this says which half
///     moved: a tier crossing, a grade improvement, or both
///     (docs/design/folder-level-progression.md §5.4).
///     <para>
///         Producer and renderers all go through <see cref="Format" /> / <see cref="TryParse" /> so
///         the wire shape lives in exactly one place. Layout is
///         <c>Folder|Tier|Grade|FromTier|FromGrade</c>, trailing halves empty when they did not move.
///     </para>
/// </summary>
public sealed record FolderProgressDetail(
    string Folder,
    int Tier,
    PhoenixLetterGrade? Grade,
    int? FromTier,
    PhoenixLetterGrade? FromGrade)
{
    private const char Separator = '|';

    /// <summary>True when the completion percent crossed a tier this time.</summary>
    public bool TierMoved => FromTier != null;

    /// <summary>True when the folder grade improved this time.</summary>
    public bool GradeMoved => FromGrade != null;

    /// <summary>Every chart in the folder is passed.</summary>
    public bool IsLamp => Tier >= FolderCompletionTier.Lamp;

    /// <summary>"60% → 80%" when the tier moved, otherwise just "80%".</summary>
    public string CompletionText => TierMoved ? $"{FromTier}% → {Tier}%" : $"{Tier}%";

    /// <summary>
    ///     "AAA → AA+" when the grade moved, "AA+" when only the tier did, null when the folder
    ///     has no grade at all. Grade names live here rather than at each call site because
    ///     several renderers need them and Razor's scope has a competing GetName extension.
    /// </summary>
    public string? GradeText => GradeMoved
        ? $"{FromGrade?.GetName()} → {Grade?.GetName()}"
        : Grade?.GetName();

    public string Format() => string.Join(Separator,
        Folder,
        Tier.ToString(),
        Grade?.GetName() ?? string.Empty,
        FromTier?.ToString() ?? string.Empty,
        FromGrade?.GetName() ?? string.Empty);

    /// <summary>
    ///     Null for anything this version cannot read, so a renderer meeting a future or malformed
    ///     payload skips the row rather than throwing — the same tolerance milestone reads already
    ///     apply to unknown kinds.
    /// </summary>
    public static FolderProgressDetail? TryParse(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return null;
        var parts = detail.Split(Separator);
        if (parts.Length != 5) return null;
        if (string.IsNullOrWhiteSpace(parts[0])) return null;
        if (!int.TryParse(parts[1], out var tier)) return null;

        int? fromTier = int.TryParse(parts[3], out var parsedFrom) ? parsedFrom : null;
        return new FolderProgressDetail(parts[0], tier, ParseGrade(parts[2]), fromTier, ParseGrade(parts[4]));
    }

    // Grades round-trip through their display name ("AA+"), which is what the enum's own parser
    // does not accept — map back through the names rather than the member names.
    private static PhoenixLetterGrade? ParseGrade(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
            if (string.Equals(grade.GetName(), value, StringComparison.OrdinalIgnoreCase))
                return grade;
        return null;
    }
}
