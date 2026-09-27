using MediatR;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Commands
{
    /// <summary>
    ///     The v1 API's import: signs in, picks the card — the one named by <paramref name="GameTag" />, else
    ///     the account's first — and imports it inside the request. It takes the same one-import slot and
    ///     five-minute cooldown as the Import button, before it touches piugame
    ///     (docs/design/import-restart-recovery.md §0).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record ImportOfficialPlayerScoresCommand(string Username, RedactedString Password, string? GameTag,
        bool IncludeBroken, MixEnum Mix = MixEnum.Phoenix) : IRequest<OfficialImportResult>;
}
