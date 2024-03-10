using System.Collections.Immutable;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public static class PhoenixTitleList
{
    private static readonly PhoenixTitle[] Titles =
    {
        new PhoenixBasicTitle("Beginner", "Default title"),
        new PhoenixDifficultyTitle("Intermediate Lv. 1", 10, 2000),
        new PhoenixDifficultyTitle("Intermediate Lv. 2", 11, 2200),
        new PhoenixDifficultyTitle("Intermediate Lv. 3", 12, 2600),
        new PhoenixDifficultyTitle("Intermediate Lv. 4", 13, 3200),
        new PhoenixDifficultyTitle("Intermediate Lv. 5", 14, 4000),
        new PhoenixDifficultyTitle("Intermediate Lv. 6", 15, 5000),
        new PhoenixDifficultyTitle("Intermediate Lv. 7", 16, 6200),
        new PhoenixDifficultyTitle("Intermediate Lv. 8", 17, 7600),
        new PhoenixDifficultyTitle("Intermediate Lv. 9", 18, 9200),
        new PhoenixDifficultyTitle("Intermediate Lv. 10", 19, 11000),
        new PhoenixDifficultyTitle("Advanced Lv. 1", 20, 13000),
        new PhoenixDifficultyTitle("Advanced Lv. 2", 20, 26000),
        new PhoenixDifficultyTitle("Advanced Lv. 3", 20, 39000),
        new PhoenixDifficultyTitle("Advanced Lv. 4", 21, 15000),
        new PhoenixDifficultyTitle("Advanced Lv. 5", 21, 30000),
        new PhoenixDifficultyTitle("Advanced Lv. 6", 21, 45000),
        new PhoenixDifficultyTitle("Advanced Lv. 7", 22, 17500),
        new PhoenixDifficultyTitle("Advanced Lv. 8", 22, 35000),
        new PhoenixDifficultyTitle("Advanced Lv. 9", 22, 52500),
        new PhoenixDifficultyTitle("Advanced Lv. 10", 22, 70000),
        new PhoenixDifficultyTitle("Expert Lv. 1", 23, 40000),
        new PhoenixDifficultyTitle("Expert Lv. 2", 23, 80000),
        new PhoenixDifficultyTitle("Expert Lv. 3", 24, 30000),
        new PhoenixDifficultyTitle("Expert Lv. 4", 24, 60000),
        new PhoenixDifficultyTitle("Expert Lv. 5", 25, 20000),
        new PhoenixDifficultyTitle("Expert Lv. 6", 25, 40000),
        new PhoenixDifficultyTitle("Expert Lv. 7", 26, 13000),
        new PhoenixDifficultyTitle("Expert Lv. 8", 26, 26000),
        new PhoenixDifficultyTitle("Expert Lv. 9", 27, 3500),
        new PhoenixDifficultyTitle("Expert Lv. 10", 27, 7000),
        new PhoenixDifficultyTitle("The Master", 28, 1900),
        new PhoenixCoOpTitle("[CO-OP] Lv.1", 30000),
        new PhoenixCoOpTitle("[CO-OP] Lv.2", 60000),
        new PhoenixCoOpTitle("[CO-OP] Lv.3", 90000),
        new PhoenixCoOpTitle("[CO-OP] Lv.4", 120000),
        new PhoenixCoOpTitle("[CO-OP] Lv.5", 150000),
        new PhoenixCoOpTitle("[CO-OP] Lv.6", 180000),
        new PhoenixCoOpTitle("[CO-OP] Lv.7", 210000),
        new PhoenixCoOpTitle("[CO-OP] Lv.8", 240000),
        new PhoenixCoOpTitle("[CO-OP] Lv.9", 270000),
        new PhoenixCoOpTitle("[CO-OP] Lv.10", 300000),
        new PhoenixCoOpTitle("[CO-OP] ADVANCED", 330000),
        new PhoenixCoOpTitle("[CO-OP] EXPERT", 360000),
        new PhoenixCoOpTitle("[CO-OP] MASTER", 390000)
    };

    public static IEnumerable<PhoenixTitleProgress> BuildProgress(IDictionary<Guid, Chart> charts,
        IEnumerable<RecordedPhoenixScore> attempts)
    {
        var progress = Titles.Select(t => new PhoenixTitleProgress(t)).ToImmutableArray();
        foreach (var attempt in attempts)
        foreach (var title in progress)
            title.ApplyAttempt(charts[attempt.ChartId], attempt);

        return progress;
    }
}