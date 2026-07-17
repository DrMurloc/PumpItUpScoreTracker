using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Events
{
    /// <summary>
    ///     Published once a weekly board actually rotates for a mix (never on the daily
    ///     retry no-op). The Discord feed reads the just-finished week and the new lineup
    ///     from the existing weekly queries.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record WeeklyChartsRotatedEvent(MixEnum Mix);
}
