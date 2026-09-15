using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

/// <summary>
///     One quarterly season as the rest of the system may know it (docs/design/seasons.md §6.1):
///     its number, its window and whether the roll has sealed it. The window is inclusive at both
///     ends and expressed in the boundary offset the calendar uses (D36), so <see cref="Holds" />
///     is the one place a play time is compared to a season.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SeasonRecord(SeasonId Id, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    DateTimeOffset? SealedAt, bool IsBalanced)
{
    /// <summary>Sealed by the roll (D13): nothing writes a row carrying this season's number again.</summary>
    public bool IsSealed => SealedAt != null;

    public bool Holds(DateTimeOffset at)
    {
        return at >= StartsAt && at <= EndsAt;
    }
}
