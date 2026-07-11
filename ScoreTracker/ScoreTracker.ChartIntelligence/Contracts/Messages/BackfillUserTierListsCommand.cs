using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Messages;

// Admin-triggered, one-time trigger: materializes every scoring user's relative tier
// lists into UserTierListEntry (UserTierListSaga, throttled). Single-mix per message,
// matching the other recompute commands.
[ExcludeFromCodeCoverage]
public sealed record BackfillUserTierListsCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
