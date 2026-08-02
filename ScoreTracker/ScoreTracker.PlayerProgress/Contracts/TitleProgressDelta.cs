namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     A not-yet-complete title's progress movement across one score batch, computed by the
///     title step of the session-snapshot pipeline.
///     <para>
///         These used to be card payload only, on the grounds that "every session nudges several
///         titles and the Sessions page would drown in gold rows". That was an argument about
///         rendering them AS gold rows, which the session breakdown does not do — it draws them
///         as bars — so they now also persist as <c>MilestoneKind.TitleProgress</c>
///         (docs/design/session-breakdown.md §2.2). The rule the old one was protecting still
///         holds: <c>MilestoneStrip</c> must never render this kind.
///     </para>
///     <para>
///         <c>Scope</c> is what a bar gets drawn per — a Phoenix difficulty level ("21"; those
///         titles span both chart types at a level, so there is no S/D split) or a Phoenix 2
///         pool ("Total" / "Singles" / "Doubles"). Empty for titles that are neither.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TitleProgressDelta(
    string Title,
    double OldPercent,
    double NewPercent,
    string Scope = "",
    double Current = 0,
    double Required = 0);
