using MediatR;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Application;

internal sealed class CycleAllImportCredentialKeysHandler : IRequestHandler<CycleAllImportCredentialKeysCommand>
{
    private readonly IImportCredentialKeyStore _keys;

    public CycleAllImportCredentialKeysHandler(IImportCredentialKeyStore keys)
    {
        _keys = keys;
    }

    public Task Handle(CycleAllImportCredentialKeysCommand request, CancellationToken cancellationToken)
    {
        return _keys.DeleteAll(cancellationToken);
    }
}
