using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     What the supplemented reading actually added to the latest sealed snapshot. The section
///     prints it while the number is still small enough to surprise someone — on a mix where
///     only a few dozen accounts have imported, a board that silently looks the same reads as
///     broken, and a count reads as honest.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSupplementedSummaryQuery(MixEnum Mix) : IQuery<SupplementedSummaryRecord>;
