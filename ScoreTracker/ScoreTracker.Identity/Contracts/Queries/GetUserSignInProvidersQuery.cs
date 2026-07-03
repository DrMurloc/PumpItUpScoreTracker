using MediatR;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     The distinct sign-in provider names connected to an account. Provider names only —
///     external ids never cross this boundary (they can identify piugame login ids).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetUserSignInProvidersQuery(Guid UserId) : IQuery<IEnumerable<string>>
{
}
