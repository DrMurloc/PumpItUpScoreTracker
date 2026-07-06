namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     A not-yet-complete title's progress movement across one score batch, computed by
///     the title step of the session-snapshot pipeline (design doc revision 2). Card
///     payload only — deliberately not a milestone: every session nudges several titles,
///     and the Sessions page would drown in gold rows.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TitleProgressDelta(string Title, double OldPercent, double NewPercent);
