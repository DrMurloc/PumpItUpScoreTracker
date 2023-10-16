using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class ApiTokenHandler : IRequestHandler<GetUserApiTokenQuery, Guid?>,
        IRequestHandler<GetUserByApiTokenQuery, User?>,
        IRequestHandler<SetApiTokenCommand, Guid>
    {
        private readonly IUserRepository _users;
        private readonly ICurrentUserAccessor _currentUser;

        public ApiTokenHandler(IUserRepository users, ICurrentUserAccessor currentUser)
        {
            _users = users;
            _currentUser = currentUser;
        }

        public async Task<Guid?> Handle(GetUserApiTokenQuery request, CancellationToken cancellationToken)
        {
            return await _users.GetUserApiToken(_currentUser.User.Id, cancellationToken);
        }

        public async Task<User?> Handle(GetUserByApiTokenQuery request, CancellationToken cancellationToken)
        {
            return await _users.GetUserByApiToken(request.ApiToken, cancellationToken);
        }

        public async Task<Guid> Handle(SetApiTokenCommand request, CancellationToken cancellationToken)
        {
            var newId = Guid.NewGuid();
            await _users.SetUserApiToken(_currentUser.User.Id, newId, cancellationToken);
            return newId;
        }
    }
}
