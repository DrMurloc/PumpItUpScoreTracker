using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The /Charts SRP search (docs/design/charts-srp.md): faceted, paged, identity-grouped.
///     Every field is optional — unset means "don't filter on this". Community facets split
///     by scoring family (Pass/Score Difficulty for Phoenix-family mixes, Community Vote for
///     XX and older); My* facets split by scoring family and never compare across it.
///     <see cref="Page" /> = null returns the entire filtered set (the CSV export path).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SearchChartsQuery : IQuery<ChartSearchResultPage>
{
    /// <summary>The visitor's mix — the linked appearance preference and single-mix scope.</summary>
    public MixEnum Mix { get; init; } = MixEnum.Phoenix;

    /// <summary>Widen scope to every mix. <see cref="Mixes" /> overrides with a custom set.</summary>
    public bool AllMixes { get; init; }

    public IReadOnlyList<MixEnum>? Mixes { get; init; }

    /// <summary>Null = anonymous: My-state is omitted and user facets are ignored.</summary>
    public Guid? UserId { get; init; }

    public string? SongNameContains { get; init; }
    public int? LevelMin { get; init; }
    public int? LevelMax { get; init; }
    public IReadOnlyList<ChartType>? Types { get; init; }
    public IReadOnlyList<int>? CoOpPlayerCounts { get; init; }
    public IReadOnlyList<SongType>? SongTypes { get; init; }
    public IReadOnlyList<string>? Artists { get; init; }
    public IReadOnlyList<string>? StepArtists { get; init; }
    public decimal? BpmMin { get; init; }
    public decimal? BpmMax { get; init; }
    public TimeSpan? DurationMin { get; init; }
    public TimeSpan? DurationMax { get; init; }
    public int? NoteCountMin { get; init; }
    public int? NoteCountMax { get; init; }
    public decimal? NpsMin { get; init; }
    public decimal? NpsMax { get; init; }

    /// <summary>Piucenter badge keys; a chart matches when ANY key is in its top-3 dominance summary.</summary>
    public IReadOnlyList<string>? Badges { get; init; }

    public IReadOnlyList<MixEnum>? DebutMixes { get; init; }
    public MixEnum? AvailableIn { get; init; }
    public MixEnum? NotAvailableIn { get; init; }
    public bool ReratedUp { get; init; }
    public bool ReratedDown { get; init; }
    public IReadOnlyList<LegacySlot>? LegacySlots { get; init; }

    public IReadOnlyList<TierListCategory>? PassDifficulty { get; init; }
    public IReadOnlyList<TierListCategory>? ScoreDifficulty { get; init; }
    public IReadOnlyList<TierListCategory>? CommunityVote { get; init; }
    public double? PassRateMin { get; init; }
    public double? ScoringLevelMin { get; init; }
    public double? ScoringLevelMax { get; init; }

    public ChartScoreStateFilter? ScoreState { get; init; }
    public PhoenixLetterGrade? PhoenixGradeMin { get; init; }
    public PhoenixPlate? PhoenixPlateMin { get; init; }
    public int? PhoenixScoreMin { get; init; }
    public int? PhoenixScoreMax { get; init; }
    public XXLetterGrade? LegacyGradeMin { get; init; }
    public DateTimeOffset? RecordedFrom { get; init; }
    public DateTimeOffset? RecordedTo { get; init; }

    /// <summary>Passed in some in-scope mix but not yet in this one — the re-clear gap.</summary>
    public MixEnum? NotReclearedIn { get; init; }

    /// <summary>Caller-supplied id restriction (saved lists ride in through this).</summary>
    public IReadOnlyList<Guid>? RestrictToChartIds { get; init; }

    public ChartSearchSort Sort { get; init; } = ChartSearchSort.Level;
    public bool SortDescending { get; init; } = true;

    /// <summary>1-based. Null = unpaged (export).</summary>
    public int? Page { get; init; } = 1;

    public int PageSize { get; init; } = 24;

    /// <summary>Fill <see cref="ChartSearchResultPage.FacetCounts" /> (the drawer asks; the export never does).</summary>
    public bool IncludeFacetCounts { get; init; }
}
