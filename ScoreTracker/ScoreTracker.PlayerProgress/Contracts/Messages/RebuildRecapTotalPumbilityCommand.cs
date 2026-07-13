using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Messages;

/// <summary>
///     Bus trigger: patch the Phoenix 2 finale's projected <c>TotalPumbility</c> on every
///     stored recap. Everything else in the payload — the Singles/Doubles projections, the
///     projected titles, and <c>ComputedAt</c> — rides through untouched. Published from the
///     admin dashboard. Only Phoenix recaps carry a projection; other mixes are a no-op.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RebuildRecapTotalPumbilityCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
