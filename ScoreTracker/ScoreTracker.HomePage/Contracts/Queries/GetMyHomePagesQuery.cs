namespace ScoreTracker.HomePage.Contracts.Queries;

/// <summary>
///     The current user's dashboard pages with their widget instances, in tab order.
///     Current-user-scoped by design — pages are private in v1 (D2), so the handler
///     resolves identity itself rather than trusting a caller-supplied id.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyHomePagesQuery : IQuery<IReadOnlyList<HomePageRecord>>;
