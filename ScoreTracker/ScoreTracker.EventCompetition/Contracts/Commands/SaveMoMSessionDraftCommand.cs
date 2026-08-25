using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     One entry of a draft as the editor holds it: the raw play, not its points — the
///     handler prices every entry under the board's frozen configuration on each save.
///     PlayedAt rides along from the journal import and is null for hand-typed entries.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMDraftEntry(Guid ChartId, int Score, PhoenixPlate Plate, bool IsBroken,
    DateTimeOffset? PlayedAt);

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
