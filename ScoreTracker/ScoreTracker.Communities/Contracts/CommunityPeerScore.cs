using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     One clubmate's score on one chart, for the Sessions page's Community Peers section.
///     <paramref name="CommunityNames" /> lists every user-created community you share with
///     them — people are commonly in two of yours, and picking one arbitrarily would read as a
///     claim about which. <paramref name="CompetitiveLevel" /> is read for the chart's own type,
///     so a doubles board sorts on doubles competitive; co-op falls back to the combined level.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityPeerScore(
    Guid UserId,
    Name PlayerName,
    IReadOnlyList<Name> CommunityNames,
    double CompetitiveLevel,
    PhoenixScore Score,
    PhoenixPlate? Plate,
    bool IsBroken);
