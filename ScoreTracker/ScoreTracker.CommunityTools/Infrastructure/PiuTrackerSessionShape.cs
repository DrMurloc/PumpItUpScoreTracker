using System.Text.Json;

namespace ScoreTracker.CommunityTools.Infrastructure;

/// <summary>
///     PIU Tracker's own wire shape, kept exactly as it was.
///     <para>
///         The integration predates this vertical by years and worked; migrating it onto the generic
///         envelope would have meant TUSA shipping a matching change on the same day or 653 players
///         losing their sync. So the seeded tool keeps the endpoint and body it has always had, and
///         the special case lives here — one named place, obviously a special case, rather than a
///         flag on the entity that invites a second one.
///     </para>
///     <para>
///         This is the whole of the divergence. Signing, retries, mix filtering, the activity log and
///         the never-persist-the-body rule are the same code every other tool gets, and the generic
///         envelope is what a new session-mode tool receives.
///     </para>
///     <para>
///         Deleting this class is a one-line change once PIU Tracker accepts the standard envelope.
///     </para>
/// </summary>
internal static class PiuTrackerSessionShape
{
    /// <summary>
    ///     Must match the id seeded by the <c>SeedPiuTrackerTool</c> migration.
    ///     <c>PiuTrackerSessionShapeTests</c> pins them together. Held in
    ///     <see cref="Domain.GrandfatheredTools" /> because the source-repository gate needs the same
    ///     id and a second copy of a well-known guid is a second thing to keep in step.
    /// </summary>
    public static readonly Guid ToolId = Domain.GrandfatheredTools.PiuTracker;

    public static bool Applies(Guid toolId)
    {
        return toolId == ToolId;
    }

    /// <summary>
    ///     <c>{base}/{gameId}/{number}</c> — the game tag split on its discriminator. A tag arrives
    ///     as "TUSA #1234"; both halves are trimmed because the space around the # is not reliable.
    /// </summary>
    public static Uri Endpoint(Uri configuredUrl, string gameTag)
    {
        var parts = gameTag.Split('#');
        var gameId = parts[0].Trim();
        var number = parts.Length > 1 ? parts[^1].Trim() : string.Empty;
        return new Uri($"{configuredUrl.ToString().TrimEnd('/')}/{Uri.EscapeDataString(gameId)}/" +
                       Uri.EscapeDataString(number));
    }

    /// <summary>The body is the session and nothing else, which is what it has always been.</summary>
    public static string Body(string sid)
    {
        return JsonSerializer.Serialize(new { sid });
    }
}
