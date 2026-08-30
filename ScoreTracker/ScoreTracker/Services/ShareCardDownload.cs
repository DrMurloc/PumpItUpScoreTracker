using ScoreTracker.Domain.Records;

namespace ScoreTracker.Web.Services;

/// <summary>Where the download stands: which image just landed, and whether the render tail started.</summary>
public sealed record ShareCardFetchProgress(int Done, int Total, bool Rendering);

/// <summary>
///     What the dialog hands its host on Download (design doc §8): the options the example
///     showed, the sink the host ticks per prefetch batch, and the token the dialog's Cancel —
///     and any close — fires. The host's loop is expected to stop on the token and swallow the
///     resulting <see cref="OperationCanceledException" />; cancellation is a user choice, not
///     a fault.
/// </summary>
public sealed record ShareCardDownloadRequest(
    ShareCardOptions Options,
    Action<ShareCardFetchProgress> Progress,
    CancellationToken Token);

/// <summary>
///     The art a composed card will make the renderer fetch — the page warms exactly this list
///     in batches, so the bar's total is the renderer's own workload and never a guess.
/// </summary>
public static class ShareCardArt
{
    /// <summary>One prefetch batch — the renderer fetches at the same bound (design doc §8).</summary>
    public const int FetchBatch = 8;

    public static IReadOnlyList<string> CollectUrls(TierListShareCard card)
    {
        return card.Rows.SelectMany(r => r.Tiles)
            .SelectMany(t => new[] { t.JacketUrl, t.GradeUrl, t.PlateUrl, t.BubbleUrl, t.ExpectedGradeUrl })
            .Append(card.BubbleUrl)
            .Where(u => u != null)
            .Select(u => u!)
            .Distinct()
            .ToArray();
    }
}
