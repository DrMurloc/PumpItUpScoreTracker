using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record ForgetImportCredentialCommand(Guid KeyId) : IRequest;
