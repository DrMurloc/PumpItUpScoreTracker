using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Application;

internal sealed class StoreImportCredentialHandler
    : IRequestHandler<StoreImportCredentialCommand, StoredImportCredential>
{
    private readonly IImportCredentialProtector _protector;
    private readonly ICurrentUserAccessor _currentUser;

    public StoreImportCredentialHandler(IImportCredentialProtector protector, ICurrentUserAccessor currentUser)
    {
        _protector = protector;
        _currentUser = currentUser;
    }

    public async Task<StoredImportCredential> Handle(StoreImportCredentialCommand request,
        CancellationToken cancellationToken)
    {
        var (keyId, ciphertext) =
            await _protector.Protect(_currentUser.User.Id, request.Username, request.Password, cancellationToken);
        return new StoredImportCredential(keyId, ciphertext);
    }
}
