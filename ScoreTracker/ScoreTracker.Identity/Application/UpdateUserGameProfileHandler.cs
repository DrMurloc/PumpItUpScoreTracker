using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Identity.Application;

/// <summary>
///     The one place an import's avatar lands, both times.
///     <para>
///         A player's avatar is stored twice: on <see cref="User.ProfileImage" />, which travels
///         into the auth cookie's claims and renders the player page, leaderboards and Discord
///         cards; and as a <c>ProfileImage</c> UI setting, which is what the static shell reads for
///         the app-bar avatar because a pre-circuit shell cannot see claims.
///     </para>
///     <para>
///         Those two writes used to sit six lines apart in OfficialLeaderboardSaga. They are here
///         instead so there is exactly one door: a rule about whose avatar wins can be applied once
///         rather than remembered in two places, and OfficialMirror — which does not own user data
///         — no longer needs to know such a rule exists (docs/design/avatar-selection.md §2).
///     </para>
/// </summary>
internal sealed class UpdateUserGameProfileHandler : IRequestHandler<UpdateUserGameProfileCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly IUserRepository _users;

    public UpdateUserGameProfileHandler(ICurrentUserAccessor currentUser, IUserRepository users, IMediator mediator)
    {
        _currentUser = currentUser;
        _users = users;
        _mediator = mediator;
    }

    public async Task Handle(UpdateUserGameProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetUser(_currentUser.User.Id, cancellationToken)
                   ?? throw new UserNotFoundException(_currentUser.User.Id);
        await _users.SaveUser(
            user with { GameTag = request.GameTag, ProfileImage = request.AvatarUrl ?? user.ProfileImage },
            cancellationToken);

        // A scrape that yielded no recognizable avatar keeps the player's existing one —
        // persisting the miss is what used to break avatars sporadically.
        if (request.AvatarUrl != null)
            await _mediator.Send(new SaveUserUiSettingCommand("ProfileImage", request.AvatarUrl.ToString()),
                cancellationToken);
    }
}
