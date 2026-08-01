using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     One mirrored PUMBILITY board's values from the latest sealed snapshot, for ranking a
///     computed pool against. Null when the mix has never swept that board.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialPumbilityBoardQuery(MixEnum Mix, string BoardName = PumbilityBoards.Combined)
    : IQuery<OfficialPumbilityBoard?>;
