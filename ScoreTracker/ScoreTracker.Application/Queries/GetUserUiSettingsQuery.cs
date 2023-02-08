using MediatR;

namespace ScoreTracker.Application.Queries;

public sealed record GetUserUiSettingsQuery : IRequest<IDictionary<string, string>>
{
}