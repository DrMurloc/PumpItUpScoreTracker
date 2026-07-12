using MediatR;

namespace ScoreTracker.Catalog.Contracts.Commands;

/// <summary>Admin resolution: binds an external key to one of our charts (Status becomes Manual).</summary>
[ExcludeFromCodeCoverage]
public sealed record ResolveExternalAliasCommand(string Source, string ExternalKey, Guid ChartId) : IRequest
{
}
