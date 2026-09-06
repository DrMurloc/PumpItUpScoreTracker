using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Fills a draft from the score journal between two stamps, inclusive. The range travels as
///     times rather than list positions, and the scores are re-read from the journal rather than
///     taken from the caller: the page chooses which plays, never what they scored.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportMoMDraftFromJournalCommand(Guid SessionId, DateTimeOffset From, DateTimeOffset To)
    : IRequest<MoMImportResult>;
