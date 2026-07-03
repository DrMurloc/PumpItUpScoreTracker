using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Identity.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetExternalLoginsQuery : IQuery<IEnumerable<ExternalLoginRecord>>
{
}
