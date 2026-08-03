using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One playable mix as a consumer sees it.
///     <para>
///         <see cref="UsesLegacyScoring" /> is the field everything else hangs off: Phoenix-era mixes
///         track a 1M-scale score with a plate, and every older mix tracks a letter grade with an
///         optional era-scale number that means something different. A consumer that reads a Fiesta EX
///         record as a Phoenix score gets a plausible, wrong answer.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MixRecord(
    MixEnum Mix,
    string Name,
    string DisplayName,
    int SortOrder,
    bool IsPrimary,
    bool UsesLegacyScoring);
