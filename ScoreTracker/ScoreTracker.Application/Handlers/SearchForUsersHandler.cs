using MediatR;
using ScoreTracker.Application.Dtos;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class SearchForUsersHandler : IRequestHandler<SearchForUsersQuery, SearchResultDto<User>>
{
    private readonly IUserRepository _userRepository;

    public SearchForUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<SearchResultDto<User>> Handle(SearchForUsersQuery request, CancellationToken cancellationToken)
    {
        var result = new List<User>();
        if (Guid.TryParse(request.SearchText, out var guidId))
        {
            var guidUser = await _userRepository.GetUser(guidId, cancellationToken);
            if (guidUser?.IsPublic ?? false) result.Add(guidUser);
        }

        var nameUsers = await _userRepository.SearchForUsersByName(request.SearchText, cancellationToken);
        result.AddRange(nameUsers.Where(u => u.IsPublic));
        return new SearchResultDto<User>(result.Skip(request.Count * (request.Page - 1)).Take(request.Count),
            result.Count);
    }
}