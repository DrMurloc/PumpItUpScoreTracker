using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Puts a draft on its board, recorded as played now. Frozen afterwards: to change it, delete
///     and record again (D17).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PublishMoMSessionCommand(Guid SessionId) : IRequest;
