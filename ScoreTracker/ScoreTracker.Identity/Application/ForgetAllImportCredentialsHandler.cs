using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Application;

internal sealed class ForgetAllImportCredentialsHandler : IRequestHandler<ForgetAllImportCredentialsCommand>
{
    private readonly IImportCredentialKeyStore _keys;
    private readonly ICurrentUserAccessor _currentUser;

    public ForgetAllImportCredentialsHandler(IImportCredentialKeyStore keys, ICurrentUserAccessor currentUser)
    {
        _keys = keys;
        _currentUser = currentUser;
    }

    public async Task Handle(ForgetAllImportCredentialsCommand request, CancellationToken cancellationToken)
    {
        await _keys.DeleteAllForUser(_currentUser.User.Id, cancellationToken);
    }
}
