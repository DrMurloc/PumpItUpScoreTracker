using MediatR;

namespace ScoreTracker.Identity.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUserUiSettingsQuery(Guid? UserId = null) : IQuery<IDictionary<string, string>>
{
}
