using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Application;

internal sealed class RevealImportCredentialHandler
    : IRequestHandler<RevealImportCredentialQuery, RevealedImportCredential?>
{
    private readonly IImportCredentialProtector _protector;
    private readonly ICurrentUserAccessor _currentUser;

    public RevealImportCredentialHandler(IImportCredentialProtector protector, ICurrentUserAccessor currentUser)
    {
        _protector = protector;
        _currentUser = currentUser;
    }

    public async Task<RevealedImportCredential?> Handle(RevealImportCredentialQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var (username, password) = await _protector.Unprotect(_currentUser.User.Id, request.KeyId,
                request.Ciphertext, cancellationToken);
            return new RevealedImportCredential(username, password);
        }
        catch (CredentialUnlockException)
        {
            return null;
        }
    }
}
