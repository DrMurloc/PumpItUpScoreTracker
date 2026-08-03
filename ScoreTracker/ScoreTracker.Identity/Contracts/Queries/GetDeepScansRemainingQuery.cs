using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>How many deep scans this account may still run before the monthly reset.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetDeepScansRemainingQuery(Guid UserId) : IQuery<int>;
