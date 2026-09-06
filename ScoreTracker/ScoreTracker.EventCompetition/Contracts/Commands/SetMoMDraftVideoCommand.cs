using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Attaches or clears the showcase video link. Verification is gone (D5), so this is a link and
///     nothing hangs on it. False means the text was not empty and not a URL either, so nothing was
///     stored — the page says so rather than clearing the field from under the player.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SetMoMDraftVideoCommand(Guid SessionId, string? Url) : IRequest<bool>;
