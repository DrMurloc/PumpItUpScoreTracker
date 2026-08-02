using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     A named best attempt. <paramref name="UserName" /> is already masked for a private
    ///     player, and <paramref name="IsPublic" /> says so outright — a consumer that needs to
    ///     hide or relabel those rows should never have to recognise the mask by its text.
    ///     <para>
    ///         <paramref name="RecordedAt" /> is when the score reached US, not when it was
    ///         played — the official site gives no play time for a best attempt. It is a tie
    ///         break, and a good one, but it is not a claim about who hit the score first in
    ///         an arcade.
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record UserPhoenixScore(Guid UserId, Guid ChartId, Name UserName, PhoenixScore Score,
        PhoenixPlate? Plate,
        bool IsBroken,
        bool IsPublic = true,
        DateTimeOffset? RecordedAt = null)
    {
    }
}
