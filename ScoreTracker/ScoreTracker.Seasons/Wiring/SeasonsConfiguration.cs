namespace ScoreTracker.Seasons.Wiring;

/// <summary>
///     The <c>Seasons</c> configuration section (docs/design/seasons.md D27), bound in Program.cs
///     and forwarded from the AppHost like the other sections.
/// </summary>
public sealed class SeasonsConfiguration
{
    /// <summary>
    ///     Whether the seasonal surfaces show for accounts that are not admins. Off by default; an
    ///     admin sees them regardless, which is how each slice is field-tested before the flag flips
    ///     for everyone (slice 6).
    /// </summary>
    public bool EnableUI { get; set; }
}
