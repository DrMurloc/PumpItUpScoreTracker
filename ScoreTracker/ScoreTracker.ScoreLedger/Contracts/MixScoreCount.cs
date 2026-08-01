using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     A mix the player could delete scores from, and how many they hold there.
///     <paramref name="IsPrimary" /> mirrors the site's own mix picker: the primary trio shows as
///     chips, everything older sits behind "More mixes" rather than being hidden.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MixScoreCount(MixEnum Mix, int ScoreCount, bool IsPrimary);
