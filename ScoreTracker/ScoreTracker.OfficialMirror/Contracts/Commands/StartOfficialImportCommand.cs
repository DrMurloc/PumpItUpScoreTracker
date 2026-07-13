using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

// Runs on the request circuit: resolves the credential to a session id, then hands the scrape to
// a background job. The password never leaves this handler.
[ExcludeFromCodeCoverage]
public sealed record StartOfficialImportCommand(ImportCredentialSource Source, MixEnum Mix, string CardId,
    string ExpectedGameTag, bool IncludeBroken, bool SyncPiuTracker) : IRequest<ImportStartResult>;
