using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Removes a session, draft or published. Discarding a draft and taking a published session off
///     a board are the same act on the same row.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DeleteMoMSessionCommand(Guid SessionId) : IRequest;
