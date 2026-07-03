using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RemoveExternalLoginCommand(string LoginProviderName, string ExternalId) : IRequest
{
}
