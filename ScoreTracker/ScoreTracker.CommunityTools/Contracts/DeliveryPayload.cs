using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Contracts;

/// <summary>
///     The body we POST to a tool. Primitives only — it is a public wire contract, so it must
///     round-trip JSON cleanly and must not carry a domain type whose serialization could change.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DeliveryPayload(
    string DeliveryId,
    int SchemaVersion,
    DateTimeOffset SentAt,
    bool Test,
    DeliveryPayload.PlayerBlock Player,
    Guid? SessionId,
    IReadOnlyList<DeliveryPayload.Change> Changes,
    string? Next)
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    ///     Who imported.
    ///     <para>
    ///         <see cref="GameTag" /> is one value rather than one per mix. The tag is an AM Pass
    ///         account setting shared across the Phoenix mixes; the per-mix rows we store are
    ///         snapshots taken by scrapes that ran on different days, not distinct identities.
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PlayerBlock(string Mix, string ScoringModel, Guid UserId, string Username,
        string? GameTag);

    /// <summary>
    ///     One changed score. Which fields carry meaning depends on the envelope's
    ///     <c>Player.ScoringModel</c>:
    ///     <list type="bullet">
    ///         <item><c>phoenix</c> — <see cref="OldScore" />/<see cref="NewScore" /> on the 1M scale, with a plate.</item>
    ///         <item>
    ///             <c>legacy</c> — <see cref="OldLetterGrade" />/<see cref="NewLetterGrade" />, and any score
    ///             present is an era-scale number that does <b>not</b> compare to a Phoenix one.
    ///         </item>
    ///     </list>
    ///     Nulls with a discriminator rather than two polymorphic shapes: easier to consume from the
    ///     dynamically typed languages most of these tools are written in.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record Change(
        Guid ChartId,
        bool IsNewPass,
        int? OldScore,
        int? NewScore,
        string? OldLetterGrade,
        string? NewLetterGrade,
        string? Plate,
        bool IsBroken);

    public static string ScoringModelOf(MixEnum mix)
    {
        return mix.UsesLegacyScoring() ? "legacy" : "phoenix";
    }
}
