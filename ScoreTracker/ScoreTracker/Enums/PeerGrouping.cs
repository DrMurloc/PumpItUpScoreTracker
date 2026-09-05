namespace ScoreTracker.Web.Enums
{
    /// <summary>
    ///     How the Play page groups the peers' pools (docs/design/pumbility-overhaul.md D36, D44):
    ///     by prevalence — how many peers hold each chart, weighted by slot — by projected gain,
    ///     which is the target list's own order in bands, or as the viewer's own top 50 by place,
    ///     split at the bar, with the peers' data on every row.
    /// </summary>
    public enum PeerGrouping
    {
        Prevalence,
        ProjectedGains
    }
}
