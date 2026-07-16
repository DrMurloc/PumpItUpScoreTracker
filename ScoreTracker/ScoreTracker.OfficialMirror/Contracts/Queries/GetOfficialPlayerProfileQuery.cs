using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>One board player's full picture: tiles, week-by-week history, and placements.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialPlayerProfileQuery(MixEnum Mix, string Username)
    : IQuery<OfficialPlayerProfileRecord?>;
