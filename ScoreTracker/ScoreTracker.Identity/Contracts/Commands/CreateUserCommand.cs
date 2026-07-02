using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record CreateUserCommand(Name Name) : IRequest<User>
{
}
