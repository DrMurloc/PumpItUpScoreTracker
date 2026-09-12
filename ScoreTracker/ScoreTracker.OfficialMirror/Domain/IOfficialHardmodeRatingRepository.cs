using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>The board players' Hardmode totals. Vertical-internal, like every mirror store.</summary>
internal interface IOfficialHardmodeRatingRepository
{
    /// <summary>
    ///     Replaces the mix's rows wholesale. A board player who dropped off every chart board
    ///     would otherwise keep a total forever, and nothing downstream could tell.
    /// </summary>
    Task Replace(MixEnum mix, IReadOnlyCollection<OfficialHardmodeRating> rows, DateTimeOffset computedAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OfficialHardmodeRow>> GetBoard(MixEnum mix, ChartType? pool,
        CancellationToken cancellationToken);
}

internal sealed record OfficialHardmodeRating(int OfficialPlayerId, double Combined, double Singles,
    double Doubles, int Held);
