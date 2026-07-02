using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record SetUserContentLockCommand(Guid UserId, bool IsLocked, Name? OverrideName) : IRequest
{
}
