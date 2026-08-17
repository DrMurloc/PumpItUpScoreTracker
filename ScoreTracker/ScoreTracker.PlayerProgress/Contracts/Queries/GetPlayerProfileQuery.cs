using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     The player page's one read. Null when there is no such player OR when the caller may not
///     look at them — the handler gates on <see cref="Domain.SecondaryPorts.IPlayerVisibilityReader" />,
///     so a private player's numbers are never one send away from a page that forgot to ask.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerProfileQuery(Guid UserId, MixEnum Mix) : IQuery<PlayerProfileRecord?>;
