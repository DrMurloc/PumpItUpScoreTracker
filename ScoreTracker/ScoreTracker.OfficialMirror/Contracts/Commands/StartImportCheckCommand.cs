using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

/// <summary>
///     Runs on the request circuit: resolves the credential to a session id, then hands the check
///     to a background job. The password never leaves the handler.
///     <para>
///         A check always imports first — counting an account that played twenty minutes ago
///         against scores we have not fetched yet reports missing charts that are simply not
///         imported yet. The reverse does not hold: a plain import never runs a check.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record StartImportCheckCommand(
    ImportCredentialSource Source,
    MixEnum Mix,
    string CardId,
    string ExpectedGameTag,
    bool DeepScan,
    /// <summary>Levels the panel asked to re-read before measuring again — the "Add these scores"
    /// button. Free: it reads only what the last check localised. Empty means no repair.</summary>
    IReadOnlyCollection<string> RepairBuckets) : IRequest<ImportCheckStartResult>;
