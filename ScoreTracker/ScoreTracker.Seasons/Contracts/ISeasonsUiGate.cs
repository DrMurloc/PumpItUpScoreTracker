namespace ScoreTracker.Seasons.Contracts;

/// <summary>
///     Whether the current request may see the seasonal surfaces (docs/design/seasons.md D27): the
///     <c>Seasons:EnableUI</c> flag, or an admin regardless of it. Every season-gated page, picker
///     entry, caption and widget asks this and nothing else, so flipping the flag is the whole launch.
///     Nothing asks it yet; slice 2a's picker is the first caller.
/// </summary>
public interface ISeasonsUiGate
{
    bool IsOpen { get; }
}
