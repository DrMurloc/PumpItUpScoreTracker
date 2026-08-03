using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     The standing verdict for a player and mix, plus how many deep scans they have left this
///     month. Read from storage — opening the page never touches piugame.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetLastImportCheckQuery(Guid UserId, MixEnum Mix) : IQuery<LastImportCheck>;
