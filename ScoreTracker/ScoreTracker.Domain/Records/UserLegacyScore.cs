using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

/// <summary>
///     A named best attempt on XX or older. The sibling of <see cref="UserPhoenixScore" />, and
///     separate from it for the reason the whole legacy model is: its score is an era score,
///     which does not fit a <see cref="PhoenixScore" /> — 76% of the scored legacy records in
///     production are above that ceiling — and its currency is the letter, which Phoenix has no
///     column for. <paramref name="UserName" /> arrives masked for a private player, exactly as
///     the Phoenix record's does.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UserLegacyScore(Guid UserId, Guid ChartId, Name UserName,
    XXLetterGrade LetterGrade, int? Score, bool IsBroken, bool IsPublic = true,
    DateTimeOffset? RecordedAt = null)
{
}
