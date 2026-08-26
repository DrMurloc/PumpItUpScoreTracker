namespace ScoreTracker.Domain.Records
{
    /// <summary>One arrow as piucenter records it: which panel, when, and which foot they think plays it.</summary>
    [ExcludeFromCodeCoverage]
    public readonly record struct StepArrow(int Panel, decimal Time, string Limb);

    /// <summary>
    ///     What a chart's arrows say about the body playing it
    ///     (docs/design/chart-identity.md §4b). Shares are 0–1.
    ///     <para>
    ///         <paramref name="Diagonal" /> is measured and deliberately never shown as a chip:
    ///         the median chart sits near 78% at every level, because the pad's corners make a
    ///         diagonal stance simply what walking in PIU looks like. It earns its place in the
    ///         drawer, beside the two shares that do discriminate.
    ///     </para>
    ///     <para>
    ///         <paramref name="Crossed" /> is the guard on <paramref name="SideOn" />: a chart can
    ///         be quiet on side-on stances and still cross your feet hard the few times it moves,
    ///         which is not the same as never turning you.
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record StanceProfile(
        bool IsDoubles,
        decimal PadShareMid4,
        decimal PadShareMid6,
        decimal Diagonal,
        decimal SideOn,
        decimal Crossed,
        decimal BracketRowShare,
        decimal RepeatedPanelShare);
}
