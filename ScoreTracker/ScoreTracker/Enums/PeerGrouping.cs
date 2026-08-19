namespace ScoreTracker.Web.Enums
{
    /// <summary>
    ///     How the Phoenix 2 Play page groups the peers' pools (docs/design/pumbility-overhaul.md
    ///     D36): by prevalence — how many peers hold each chart, weighted by slot — or by projected
    ///     gain, which is the target list's own order in bands.
    /// </summary>
    public enum PeerGrouping
    {
        Prevalence,
        ProjectedGains
    }
}
