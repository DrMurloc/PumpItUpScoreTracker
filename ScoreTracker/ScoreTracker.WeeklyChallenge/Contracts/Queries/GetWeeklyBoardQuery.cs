using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>
///     The weekly page's single board read (replacing its charts + entries + user-lookup
///     cascade; the raw queries remain for widgets and the partner API). <c>WeekStart</c> null
///     reads the live board; a past rotation date (from <c>GetPastWeeklyDatesQuery</c>) reads
///     that finished week. <c>UserId</c> adds the caller's rows and suggestion flags;
///     <c>OnlyUserIds</c> scopes every board to a community's members. Only mixes with weekly
///     boards (Phoenix, Phoenix 2) have data — others return an empty view.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetWeeklyBoardQuery(
    MixEnum Mix,
    DateTimeOffset? WeekStart = null,
    Guid? UserId = null,
    IReadOnlyList<Guid>? OnlyUserIds = null) : IQuery<WeeklyBoardView>;
