using System.Text.Json;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Dev-harness raw table transfer (2026-07 local-dev track). Exposes allowlisted
///     tables as raw rows for the /dev/export endpoints and replays them into a local
///     database.
///     <para>
///         **Deliberately unstable**: row shapes are the physical table shapes and shift —
///         including breaking changes — whenever the schema does. Never surface these
///         through the partner API contracts, never pin them in wire-shape tests, and
///         integrators must not consume them.
///     </para>
/// </summary>
public interface IDevDataTransfer
{
    /// <summary>Allowlisted reference-table keys, ordered FK-safe for import.</summary>
    IReadOnlyList<string> ReferenceTableKeys { get; }

    Task<IReadOnlyList<Dictionary<string, object?>>> ExportReferenceRows(string tableKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Dictionary<string, object?>>> ExportUserScores(Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Replaces every allowlisted reference table with the supplied rows in one
    ///     transaction (deletes in reverse FK order, inserts forward).
    /// </summary>
    Task ReplaceReferenceTables(
        IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, JsonElement>>> rowsByTable,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Replaces the given local user's score rows with the supplied rows; the UserId
    ///     column is rewritten to <paramref name="localUserId" /> (exported rows carry the
    ///     remote site's user id).
    /// </summary>
    Task ReplaceUserScores(Guid localUserId,
        IReadOnlyList<Dictionary<string, JsonElement>> rows,
        CancellationToken cancellationToken = default);
}
