using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Every chart that moved, arrived or left between two mixes. Any pair the catalog
///     knows, not just XX to Phoenix — the same three questions answer for all of them.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMixDiffQuery(MixEnum From, MixEnum To) : IQuery<MixDiffRecord>
{
}
