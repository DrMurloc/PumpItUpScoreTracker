using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Identity.Application;

/// <summary>
///     Pinning, unpinning, and reading back which of the two an account is in.
///     <para>
///         Every write here goes to both places an avatar is stored — the User row for the claims
///         and the ProfileImage UI setting for the static shell's app-bar avatar — because a write
///         that reached only one would leave the app bar disagreeing with the rest of the site
///         (docs/design/avatar-selection.md §2).
///     </para>
/// </summary>
internal sealed class AvatarSaga :
    IRequestHandler<PinAvatarCommand>,
    IRequestHandler<UnpinAvatarCommand>,
    IRequestHandler<GetMyAvatarQuery, MyAvatarRecord>
{
    /// <summary>
    ///     The only place a pinned avatar may point. A profile picture is rendered on other
    ///     players' screens, so it must come from our own CDN rather than an address a player
    ///     supplied — otherwise the field is an open image embed with a request log attached.
    ///     <para>
    ///         A prefix check rather than a catalog lookup, deliberately: it keeps Identity from
    ///         needing a reference to Catalog, and a url on our own avatar CDN that is not in the
    ///         catalog is still an avatar, still ours, and harmless.
    ///     </para>
    /// </summary>
    private const string AllowedPrefix = "https://piuimages.arroweclip.se/avatars/";

    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly IUserRepository _users;

    public AvatarSaga(ICurrentUserAccessor currentUser, IUserRepository users, IMediator mediator)
    {
        _currentUser = currentUser;
        _users = users;
        _mediator = mediator;
    }

    public async Task Handle(PinAvatarCommand request, CancellationToken cancellationToken)
    {
        if (!IsAllowed(request.ImageUrl)) throw new InvalidAvatarException();

        var user = await Current(cancellationToken);
        await Show(user with { ProfileImage = request.ImageUrl, AvatarIsPinned = true },
            cancellationToken);
    }

    public async Task Handle(UnpinAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await Current(cancellationToken);

        // Restoring what the last import saw is the whole point of keeping it. An account with
        // nothing recorded — never imported, and created after the backfill — simply keeps what
        // it is wearing rather than falling back to stock art nobody asked for.
        var restored = user.ImportedProfileImage ?? user.ProfileImage;
        await Show(user with { ProfileImage = restored, AvatarIsPinned = false }, cancellationToken);
    }

    public async Task<MyAvatarRecord> Handle(GetMyAvatarQuery request, CancellationToken cancellationToken)
    {
        var user = await Current(cancellationToken);
        return new MyAvatarRecord(user.ProfileImage, user.AvatarIsPinned, user.ImportedProfileImage);
    }

    private static bool IsAllowed(Uri url)
    {
        return url.IsAbsoluteUri
               && url.ToString().StartsWith(AllowedPrefix, StringComparison.Ordinal);
    }

    private async Task<User> Current(CancellationToken cancellationToken)
    {
        return await _users.GetUser(_currentUser.User.Id, cancellationToken)
               ?? throw new UserNotFoundException(_currentUser.User.Id);
    }

    /// <summary>Writes the avatar to both of the places it is stored.</summary>
    private async Task Show(User user, CancellationToken cancellationToken)
    {
        await _users.SaveUser(user, cancellationToken);
        await _mediator.Send(new SaveUserUiSettingCommand("ProfileImage", user.ProfileImage.ToString()),
            cancellationToken);
    }
}
