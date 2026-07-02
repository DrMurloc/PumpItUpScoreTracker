using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record SetUserContentLockCommand(Guid UserId, bool IsLocked, Name? OverrideName) : IRequest
{
}
