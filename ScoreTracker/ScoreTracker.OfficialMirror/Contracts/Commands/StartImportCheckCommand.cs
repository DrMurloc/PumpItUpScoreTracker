using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

/// <summary>
///     Runs on the request circuit: resolves the credential to a session id, then hands the check
///     to a background job. The password never leaves the handler.
///     <para>
///         A check always imports first — counting an account that played twenty minutes ago
///         against scores we have not fetched yet reports charts that are simply not imported yet.
///         The reverse does not hold: a plain import never runs a check.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record StartImportCheckCommand(
    ImportCredentialSource Source,
    MixEnum Mix,
    string CardId,
    string ExpectedGameTag,
    /// <summary>Read every page of the best-score list instead of counting levels. Costs one of
    /// the month's allowance, and is the only way to find a score improved without changing grade
    /// or plate.</summary>
    bool DeepScan,
    /// <summary>The player's broken-scores choice, same as an import's. A repair that ignored it
    /// would skip exactly the charts their imports save, while telling them their account is
    /// complete.</summary>
    bool IncludeBroken) : IRequest<ImportCheckStartResult>;
