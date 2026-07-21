using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     A site account's standing on the official boards, resolved through the import-stamped
///     player link. Null when the account has no linked mirror player (never imported via
///     PIUGAME) — consumers omit their official tiles in that case.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialPlayerStandingQuery(MixEnum Mix, Guid UserId)
    : IQuery<OfficialPlayerStandingRecord?>;
