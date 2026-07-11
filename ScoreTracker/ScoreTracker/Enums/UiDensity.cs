namespace ScoreTracker.Web.Enums
{
    // The three sanctioned collection densities (UX rule 5): cards, the jacket
    // sticker sheet, and rows. Persisted per page (Density__<Page>) per the
    // tier-lists overhaul round-2 decision; a page never invents a fourth mode.
    public enum UiDensity
    {
        Comfortable,
        Compact,
        Table
    }
}
