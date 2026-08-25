namespace ScoreTracker.SharedKernel.Enums;

/// <summary>
///     Which half of a shared two-chart video a chart occupies. Stored data, never derived at
///     render time: the video's layout is fixed content, so a mix switch relabels the levels
///     but never swaps the sides (docs/design/video-sides.md).
/// </summary>
public enum VideoSide
{
    Left,
    Right
}
