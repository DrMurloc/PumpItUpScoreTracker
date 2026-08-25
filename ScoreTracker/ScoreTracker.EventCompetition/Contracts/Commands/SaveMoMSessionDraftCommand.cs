using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Creates or replaces the caller's draft on a board (SessionId null = create; one open
///     draft per board per player, §10). The whole entry list is authoritative — the handler
///     replays it in order under the board's rules, so a repeat or a window overflow is
///     rejected with a domain exception rather than stored. Saving is what makes thirty
///     hand-typed charts survive a stray navigation (D17); it never publishes.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SaveMoMSessionDraftCommand(Guid BoardId, Guid? SessionId,
    IReadOnlyList<MoMDraftEntry> Entries, Uri? VideoUrl) : IRequest<MoMSessionView>;
