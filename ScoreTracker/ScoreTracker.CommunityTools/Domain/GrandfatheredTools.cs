namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     Tools that predate the source-repository requirement and are exempt from it.
///     <para>
///         Exempt by id rather than by a date. <c>SeedPiuTrackerTool</c> stamps
///         <c>SYSDATETIMEOFFSET()</c>, so PIU Tracker's <c>CreatedAt</c> lands whenever the migration
///         runs — at deploy time, alongside the real makers registering that same week. Any
///         "created before X" cutoff would be a coin flip between them.
///     </para>
///     <para>
///         PIU Tracker arrived Public with 653 migrated players and a maker who has not supplied a
///         repository. Gating it would take a working integration away from those players to enforce
///         a rule written after they connected. When the URL arrives, delete the entry — the list is
///         meant to shrink and nothing may be added to it without the owner saying so.
///     </para>
/// </summary>
internal static class GrandfatheredTools
{
    /// <summary>
    ///     PIU Tracker. Also <c>PiuTrackerSessionShape.ToolId</c> and the seed migration;
    ///     <c>PiuTrackerSessionShapeTests</c> pins them together.
    /// </summary>
    public static readonly Guid PiuTracker = Guid.Parse("7b1b7f8e-6f1e-4c4b-9f3e-2c0d5a9e4b10");

    private static readonly HashSet<Guid> All = new() { PiuTracker };

    public static bool Exempt(Guid toolId)
    {
        return All.Contains(toolId);
    }
}
