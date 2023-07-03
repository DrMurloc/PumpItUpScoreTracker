using System.Collections.Immutable;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public static class PhoenixTitleList
{
    private static readonly PhoenixTitle[] Titles =
    {
        new PhoenixBasicTitle("Beginner", "Default title")
    };

    public static IEnumerable<PhoenixTitleProgress> BuildProgress(IEnumerable<RecordedPhoenixScore> attempts)
    {
        var progress = Titles.Select(t => new PhoenixTitleProgress(t)).ToImmutableArray();
        foreach (var attempt in attempts)
        foreach (var title in progress)
            title.ApplyAttempt(attempt);

        return progress;
    }
}