using MediatR;

namespace ScoreTracker.Application.Queries;

public sealed record GetUserUiSettingsQuery(Guid? UserId = null) : IRequest<IDictionary<string, string>>
{
}