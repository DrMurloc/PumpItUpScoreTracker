using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUnresolvedAliasesQuery(string Source = ExternalAliasSources.PiuCenter)
    : IQuery<IReadOnlyList<UnresolvedAliasRecord>>
{
}
