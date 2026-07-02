using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Identity.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUserByExternalLoginQuery(string ExternalId, string LoginProviderName) : IQuery<User?>
{
}
