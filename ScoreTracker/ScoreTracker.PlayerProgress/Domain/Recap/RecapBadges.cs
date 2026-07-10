using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Domain.Recap;

/// <summary>
///     Threshold logic for every recap badge. The thresholds were calibrated against
///     production data on 2026-07-09 and are documented in
///     docs/design/phoenix-season-recap.md — retune them there first.
/// </summary>
internal static class RecapBadges
{
    /// <summary>
    ///     Ladder shares are strict ("more than 50%") and measured against the FULL
    ///     title list including site-detected titles — title collection is personal
    ///     progress, deliberately not population-tuned.
    /// </summary>
    public static RecapBadge? CollectionBadge(int earnedTitles, int totalTitles)
    {
        if (totalTitles <= 0) return null;
        var share = earnedTitles / (double)totalTitles;
        if (share > .95) return RecapBadge.LeaveSomeTitlesForTheRestOfUs;
        if (share > .90) return RecapBadge.TitleMaster;
        if (share > .75) return RecapBadge.TitleCollector;
        if (share > .50) return RecapBadge.TitleHunter;
        return null;
    }

    /// <summary>
    ///     Only Single and Double folders count (S15 and D15 are two folders); the
    ///     90% floor is inclusive, matching the FolderCompletion90 highlight flag.
    /// </summary>
    public static int CountFoldersOver90(IEnumerable<RecapFolder> folders)
    {
        return folders.Count(f =>
            f.Size > 0 &&
            (f.Type == ChartType.Single || f.Type == ChartType.Double) &&
            f.Passed >= .9 * f.Size);
    }

    public static RecapBadge? CompletionistBadge(int foldersOver90)
    {
        if (foldersOver90 >= 40) return RecapBadge.YouKnowPumpItUpDoesntDoLamps;
        if (foldersOver90 >= 30) return RecapBadge.CompletionistUltra;
        if (foldersOver90 >= 20) return RecapBadge.CompletionistSupreme;
        if (foldersOver90 >= 10) return RecapBadge.CompletionistPlus;
        if (foldersOver90 >= 5) return RecapBadge.Completionist;
        return null;
    }

    public static RecapBadge? CoOpBadge(int totalX2Charts, int passedX2Charts)
    {
        if (totalX2Charts <= 0) return null;
        if (passedX2Charts >= totalX2Charts) return RecapBadge.IHopeYouHeldHandsOnCanonD;
        var share = passedX2Charts / (double)totalX2Charts;
        if (share > .90) return RecapBadge.FriendshipIsMagic;
        if (share > .75) return RecapBadge.ClearlyHasFriends;
        if (share > .50) return RecapBadge.Socialite;
        return null;
    }

    /// <summary>
    ///     Matches the calibrated artist set: BanYa, Banya Production, YAHPP, and
    ///     their collab credits. msgoon is deliberately excluded (owner call).
    /// </summary>
    public static bool IsBanYaArtist(Name? artist)
    {
        if (artist is null) return false;
        var text = artist.Value.ToString();
        return text.Contains("banya", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("yahpp", StringComparison.OrdinalIgnoreCase);
    }

    public static RecapBadge? BanYaBadge(int totalBanYaCharts, int passedBanYaCharts)
    {
        if (totalBanYaCharts <= 0) return null;
        return passedBanYaCharts / (double)totalBanYaCharts > .5 ? RecapBadge.BanYaLover : null;
    }

    public static bool EarnsBigFeet(RecordedPhoenixScore? uhHeungSingles22)
    {
        return uhHeungSingles22 is { IsBroken: false, Score: not null } &&
               (int)uhHeungSingles22.Score.Value >= (int)PhoenixLetterGrade.SSSPlus.GetMinimumScore();
    }

    /// <summary>
    ///     Mash-grade passes (AA+ or lower) must cover more than 75% of the S24+
    ///     folder on their own; higher-graded passes neither count nor disqualify.
    ///     Passes with no recorded score can't prove a grade and don't count,
    ///     matching the calibration query.
    /// </summary>
    public static bool EarnsGrandMashter(IEnumerable<RecordedPhoenixScore> singles24PlusRecords,
        int totalSingles24PlusCharts)
    {
        if (totalSingles24PlusCharts <= 0) return false;
        var mashCap = (int)PhoenixLetterGrade.AAPlus.GetMaximumScore();
        var mashPasses = singles24PlusRecords.Count(r =>
            r is { IsBroken: false, Score: not null } && (int)r.Score.Value <= mashCap);
        return mashPasses > .75 * totalSingles24PlusCharts;
    }

    public static bool EarnsNowYouCanPlayTheGame(IEnumerable<DifficultyLevel> passedDoublesLevels)
    {
        return passedDoublesLevels.Any(level => (int)level >= 28);
    }

    /// <summary>Exact, case-sensitive tag match; the dove emoji is the Web layer's job.</summary>
    public static bool EarnsDove(string? gameTag)
    {
        return gameTag == "DULKI #2827";
    }

    /// <summary>The rarest earned title, when fewer than 1% of titled players hold it.</summary>
    public static SnowflakeTitle? Snowflake(IEnumerable<Name> earnedTitles,
        IReadOnlyDictionary<Name, int> holdersByTitle, int titledUserCount)
    {
        var rarest = RarestTitles(earnedTitles, holdersByTitle, titledUserCount, 1);
        return rarest.Count > 0 && rarest[0].HolderShare < .01 ? rarest[0] : null;
    }

    /// <summary>
    ///     Earned titles missing from the aggregation are skipped rather than treated
    ///     as rare — a stale rarity cache must not mint snowflakes.
    /// </summary>
    public static IReadOnlyList<SnowflakeTitle> RarestTitles(IEnumerable<Name> earnedTitles,
        IReadOnlyDictionary<Name, int> holdersByTitle, int titledUserCount, int count)
    {
        if (titledUserCount <= 0) return Array.Empty<SnowflakeTitle>();
        return earnedTitles
            .Where(holdersByTitle.ContainsKey)
            .Select(title => new SnowflakeTitle(title, holdersByTitle[title] / (double)titledUserCount))
            .OrderBy(s => s.HolderShare)
            .ThenBy(s => s.Title.ToString(), StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToArray();
    }

    public static IReadOnlyDictionary<PhoenixPlate, int> PlateCabinet(IEnumerable<RecordedPhoenixScore> records)
    {
        return records
            .Where(r => r is { IsBroken: false, Plate: not null })
            .GroupBy(r => r.Plate!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
