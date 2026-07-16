using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

/// <summary>
///     Admin trigger: wipe a mix's weekly highlights and record books, then replay every
///     sealed snapshot under the current highlight rules.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RebuildWeeklyHighlightsCommand(MixEnum Mix);
