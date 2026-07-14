using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

// Breach lever: deletes every wrapped-credential key. Each device's stored blob becomes
// permanently un-decryptable at once (no wrapped key -> no decrypt); users simply re-enter on
// their next import.
[ExcludeFromCodeCoverage]
public sealed record CycleAllImportCredentialKeysCommand : IRequest;
