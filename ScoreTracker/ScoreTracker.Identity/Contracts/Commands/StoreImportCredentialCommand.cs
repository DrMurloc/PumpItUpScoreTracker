using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record StoreImportCredentialCommand(RedactedString Username, RedactedString Password)
    : IRequest<StoredImportCredential>;
