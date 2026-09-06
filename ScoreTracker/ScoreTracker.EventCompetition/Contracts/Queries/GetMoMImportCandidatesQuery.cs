using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     The import dialog's contents: recent plays from the score journal, split into blocks at the
///     long gaps, with the checks for the selected range. Omit the range to open on the block the
///     detector suggests.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMImportCandidatesQuery(Guid SessionId, int? StartIndex = null, int? EndIndex = null)
    : IQuery<MoMImportCandidates?>;
