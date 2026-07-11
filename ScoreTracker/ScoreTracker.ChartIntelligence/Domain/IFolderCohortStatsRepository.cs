using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

internal interface IFolderCohortStatsRepository
{
    /// <summary>Replace-folder semantics: one save swaps every bucket row for the folder.</summary>
    Task SaveFolder(MixEnum mix, ChartType chartType, int level, IEnumerable<FolderCohortBucketRecord> buckets,
        CancellationToken cancellationToken);

    Task<IEnumerable<FolderCohortBucketRecord>> GetBuckets(MixEnum mix, ChartType chartType, int level,
        CancellationToken cancellationToken);
}

/// <summary>
///     One competitive-level bucket of a folder's pass-count distribution. Bucket is the
///     competitive level doubled and rounded (half-level granularity), so read-time merges
///     can reproduce the ±0.5 "similar players" window. Histogram maps passes → players.
/// </summary>
internal sealed record FolderCohortBucketRecord(int Bucket, IReadOnlyDictionary<int, int> PassHistogram);
