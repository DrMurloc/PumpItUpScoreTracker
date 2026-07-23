namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     What a journal row meant for the player's record. The journal writes only on
///     state change (progress-only), so new rows are always one of the first three;
///     Played only appears on legacy rows written before the guard.
/// </summary>
public enum ScoreEventClassification
{
    /// <summary>
    ///     First unbroken best for the chart on its mix. A first pass on a newer version
    ///     (e.g. first Phoenix 2 pass) is a NewPass even when an earlier-version best exists;
    ///     that earlier best rides along as a carryover for display ("+X from Phoenix").
    /// </summary>
    NewPass,

    /// <summary>Score (or plate at the same score) improved over the prior best on the same mix.</summary>
    Upscore,

    /// <summary>A broken entry — first for the chart, or a broken-best improvement.</summary>
    Break,

    /// <summary>Legacy no-op row from before the progress-only guard.</summary>
    Played
}
