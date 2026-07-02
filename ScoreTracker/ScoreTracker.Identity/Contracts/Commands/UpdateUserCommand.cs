using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record UpdateUserCommand(Name newName, bool newIsPublic, Name? newCountry) : IRequest
{
}
