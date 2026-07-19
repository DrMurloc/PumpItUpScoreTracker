using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Events
{
    /// <summary>
    ///     Published when an official leaderboard sweep seals a snapshot for a mix. The
    ///     Discord digest reads the sealed week's highlights and cutlines; a baseline seal
    ///     (the first run, which only primes records) carries <see cref="IsBaseline" /> so
    ///     the feed can skip it.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record OfficialSnapshotSealedEvent(MixEnum Mix, bool IsBaseline);
}
