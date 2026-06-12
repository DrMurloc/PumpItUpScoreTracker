using MediatR;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUserUiSettingsQuery(Guid? UserId = null) : IQuery<IDictionary<string, string>>
{
}
