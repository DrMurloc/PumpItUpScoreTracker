using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Application;

internal sealed class ForgetImportCredentialHandler : IRequestHandler<ForgetImportCredentialCommand>
{
    private readonly IImportCredentialKeyStore _keys;
    private readonly ICurrentUserAccessor _currentUser;

    public ForgetImportCredentialHandler(IImportCredentialKeyStore keys, ICurrentUserAccessor currentUser)
    {
        _keys = keys;
        _currentUser = currentUser;
    }

    public async Task Handle(ForgetImportCredentialCommand request, CancellationToken cancellationToken)
    {
        await _keys.Delete(request.KeyId, _currentUser.User.Id, cancellationToken);
    }
}
