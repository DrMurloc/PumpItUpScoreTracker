namespace ScoreTracker.Web.Enums
{
    /// <summary>
    ///     How the Play page groups the peers' pools (docs/design/pumbility-overhaul.md D36, D66): by
    ///     prevalence — how many peers hold each chart, weighted by slot — by projected gain, which is
    ///     the target list's own order in bands, or by rarity — every chart of the levels at least half
    ///     of the peers keep a chart of, the fewest keepers first.
    /// </summary>
    public enum PeerGrouping
    {
        Prevalence,
        ProjectedGains,
        Rarity
    }
}
