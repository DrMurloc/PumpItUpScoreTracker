using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Attaches or clears the showcase video link. Verification is gone (D5), so this is a link and
///     nothing hangs on it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SetMoMDraftVideoCommand(Guid SessionId, string? Url) : IRequest;
