using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     One row of a mirrored chart board.
    ///     <para>
    ///         <paramref name="AvatarUrl" /> is NULLABLE, and a null means "this row taught us
    ///         nothing about their picture" — not "they have none". The sweep used to substitute a
    ///         stock avatar here, which the player upsert then wrote over a perfectly good mirrored
    ///         one whenever a URL failed to parse. The fallback belongs at display time, where it
    ///         costs one render, rather than in storage, where it costs the real picture.
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record OfficialChartLeaderboardEntry(string Username, Chart Chart, PhoenixScore Score,
        Uri? AvatarUrl)
    {
    }
}
