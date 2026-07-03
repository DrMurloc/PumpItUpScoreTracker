using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Identity.Application;

internal sealed class GetUserSignInProvidersHandler : IRequestHandler<GetUserSignInProvidersQuery, IEnumerable<string>>
{
    private readonly IUserRepository _users;

    public GetUserSignInProvidersHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<IEnumerable<string>> Handle(GetUserSignInProvidersQuery request,
        CancellationToken cancellationToken)
    {
        return (await _users.GetExternalLogins(request.UserId, cancellationToken))
            .Select(l => l.LoginProviderName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
