using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>One counting play on the public board. Carries no photo — the field never sees them.</summary>
[ExcludeFromCodeCoverage]
public sealed record QualifierPlay(Chart Chart, PhoenixScore Score, double Rating, SubmissionSource Source);

[ExcludeFromCodeCoverage]
public sealed record QualifierEntry(
    Name UserName,
    bool HasAccount,
    double Total,
    IReadOnlyList<QualifierPlay> Plays);

/// <summary>
///     Everything the player page renders. Entrants who have registered but posted nothing sit in
///     <see cref="WithoutScores" /> rather than holding a rank.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QualifierBoard(
    QualifiersConfiguration Configuration,
    Name TournamentName,
    IReadOnlyList<QualifierEntry> Ranked,
    IReadOnlyList<Name> WithoutScores,
    QualifierStanding? Yours,
    bool AutoSubmitEnabled,
    bool IsClosed,
    IReadOnlyList<Guid> SuggestedChartIds);

/// <summary>Where the viewer sits, and what it would take to move up one.</summary>
[ExcludeFromCodeCoverage]
public sealed record QualifierStanding(
    Name UserName,
    int Place,
    int FieldSize,
    double Total,
    double? GapToNext,
    Name? NextUp);

/// <summary>The organiser's view of one submission — this is the only place a photo appears.</summary>
[ExcludeFromCodeCoverage]
public sealed record QualifierAdminPlay(
    Chart Chart,
    PhoenixScore Score,
    double Rating,
    SubmissionSource Source,
    Uri? PhotoUrl,
    DateTimeOffset SubmittedAt);

[ExcludeFromCodeCoverage]
public sealed record QualifierAdminEntry(
    Name UserName,
    bool HasAccount,
    double Total,
    DateTimeOffset FirstSeen,
    IReadOnlyList<QualifierAdminPlay> Plays);

/// <summary>
///     Entries that look like the same person twice. Flagged when names normalize together and at
///     least one side is signed in — which is the shape a player leaves behind when they submit
///     anonymously and then come back with an account.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QualifierDuplicateGroup(IReadOnlyList<QualifierAdminEntry> Entries);

[ExcludeFromCodeCoverage]
public sealed record QualifierAdminView(
    QualifiersConfiguration Configuration,
    Name TournamentName,
    IReadOnlyList<QualifierAdminEntry> Entries,
    IReadOnlyList<Name> WithoutScores,
    IReadOnlyList<QualifierDuplicateGroup> Duplicates)
{
    public int PhotoCount => Entries.Sum(e => e.Plays.Count(p => p.Source == SubmissionSource.Manual));
}
