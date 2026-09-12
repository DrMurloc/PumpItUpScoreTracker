using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Messages;

/// <summary>
///     Rebuild the Hardmode chart list for one mix (docs/design/hardmode-leaderboard.md).
///     Published weekly by RecurringJobRunner after the official import seals, and by the
///     /Admin button for the first run.
///     <para>
///         Single-mix per message, like every other compute trigger here: replaying one mix's
///         census never touches the other's. Phoenix 2 is the only mix with a census today, and
///         a mix whose population cannot fill a single pool writes nothing rather than a list
///         of everything.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RebuildHardmodeChartsCommand(MixEnum Mix = MixEnum.Phoenix2);
