using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record UpdateUserCommand(Name newName, bool newIsPublic, Name? newCountry) : IRequest
{
}
