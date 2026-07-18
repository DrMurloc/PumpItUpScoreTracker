using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

/// <summary>
///     Admin trigger: re-scrape the play ranking alone and re-attach it to the latest
///     sealed snapshot — minutes instead of a full board sweep. The popularity rows and
///     the "Popularity" tier list refresh; placements, highlights, and the seal stay
///     untouched.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RefreshPopularityCommand(MixEnum Mix);
