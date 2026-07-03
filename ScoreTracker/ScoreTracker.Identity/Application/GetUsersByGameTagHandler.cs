using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Identity.Application;

internal sealed class GetUsersByGameTagHandler : IRequestHandler<GetUsersByGameTagQuery, IEnumerable<User>>
{
    private readonly IUserRepository _users;

    public GetUsersByGameTagHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<IEnumerable<User>> Handle(GetUsersByGameTagQuery request, CancellationToken cancellationToken)
    {
        return await _users.GetUsersByGameTag(request.GameTag, cancellationToken);
    }
}
