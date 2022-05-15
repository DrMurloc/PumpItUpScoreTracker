using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands;

public sealed record UpdateUserCommand(Name newName, bool newIsPublic) : IRequest
{
}