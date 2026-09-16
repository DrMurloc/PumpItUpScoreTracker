using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     The viewer's own Hardmode standing, for the Sessions-page banner (D31). Named for the viewer
///     rather than any user: a private player's place is theirs to see, so a caller asks about the
///     person looking and never about somebody else.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetHardmodeStandingQuery(MixEnum Mix, Guid ViewerId) : IQuery<HardmodeStandingRecord>;
