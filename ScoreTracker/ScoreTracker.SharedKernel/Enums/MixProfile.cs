namespace ScoreTracker.SharedKernel.Enums;

/// <summary>How a mix scores a play: the 1,000,000-point Phoenix formula, or the era model of letter + broken flag.</summary>
public enum ScoringModel
{
    Phoenix,
    Legacy
}

/// <summary>Which score → grade cutoff table a mix reads. Grade identity is shared; only the floors differ.</summary>
public enum GradeLadder
{
    Phoenix1,
    Phoenix2,

    /// <summary>Nine grades and no plus tiers (docs/design/rise.md §5.2).</summary>
    Rise
}

/// <summary>What a mix hands out beside the grade.</summary>
public enum AwardSet
{
    /// <summary>The eight Phoenix plates, Rough Game through Perfect Game.</summary>
    PhoenixPlates,

    /// <summary>
    ///     Rise's three marks — Perfect Game, Full Combo, No Miss — stored as the Phoenix plates they
    ///     coincide with and shown under their own names (docs/design/rise.md D5).
    /// </summary>
    RiseMarks,
    None
}

/// <summary>What the player stands on. A clear on one platform says nothing about the other.</summary>
public enum Platform
{
    Pad,
    Keyboard
}

/// <summary>
///     Where a mix's picture assets live under the image CDN: the difficulty-bubble folder and the
///     letter/award folder, each null for the flat root set (the XX bubbles, the Phoenix letters and
///     plates) that every mix drew before per-mix art existed. A bubble set that has no half-double
///     stepball draws half-doubles as the neutral chip instead.
/// </summary>
public sealed record MixArt(string? BubbleFolder, string? ScoreArtFolder, bool HasHalfDoubleBubble)
{
    public static readonly MixArt Flat = new(null, null, false);
}

/// <summary>
///     The nav's honest answers for a mix: whether it is fair to put each link in front of a
///     player of it (see <see cref="MixCapabilities" /> for why these are not a route gate).
/// </summary>
public sealed record MixFeatures(
    bool Pumbility,
    bool OfficialBoards,
    bool WeeklyBoard,
    bool MarchOfMurlocs,
    bool PhoenixCalculators)
{
    public static readonly MixFeatures Phoenix = new(true, true, true, true, true);
    public static readonly MixFeatures None = new(false, false, false, false, false);
}

/// <summary>
///     What a mix <i>is</i>. One boolean — <c>UsesLegacyScoring()</c> — used to answer every
///     question about a mix, and it only ever answered one of them; a Phoenix-scored mix with no
///     official site, no lifebar and its own grade ladder gets every other answer wrong off it.
///     Each field here is one of those questions, answered on its own, so a caller asks the
///     question it means (docs/design/rise.md §3).
///     <para>
///         <c>SittingWindow</c> is how the plays endpoint gathers a player's plays on the mix: how
///         far apart in play time two plays can be and still share a sitting, and how long a sitting
///         stays open with nothing arriving before it closes on its own (docs/design/rise.md §12).
///     </para>
/// </summary>
public sealed record MixProfile(
    ScoringModel ScoringModel,
    GradeLadder GradeLadder,
    AwardSet Awards,
    MixEnum? OfficialSite,
    Platform Platform,
    bool HasLifebarModel,
    IReadOnlyList<ChartType> ChartTypes,
    MixArt Art,
    MixFeatures Features,
    TimeSpan SittingWindow);

/// <summary>
///     The profile of every mix. Adding a mix is a row here, its enum value, a <c>MixIds</c> Guid
///     and a <c>scores.Mix</c> row; a DomainTest asserts every enum value has its row.
/// </summary>
public static class MixProfiles
{
    // Every mix before Phoenix: era scoring, no site the tracker reads, the flat art set, and
    // none of the Phoenix-generation features. XX is a legacy mix here even though xx.piugame.com
    // exists — the tracker never reads it (PiuGameConfiguration).
    // Every chart type the series has ever had. The performance types end at XX and the
    // six-panel half-doubles at the Infinity/Pro line, but a picker hides a folder with no
    // charts in it, so the legacy profile declares the superset instead of guessing which of
    // twenty-eight mixes had which (docs/design/rise.md §3.1).
    private static readonly IReadOnlyList<ChartType> EveryLegacyType = new[]
    {
        ChartType.Single, ChartType.Double, ChartType.CoOp,
        ChartType.SinglePerformance, ChartType.DoublePerformance, ChartType.HalfDouble
    };

    private static readonly IReadOnlyList<ChartType> SingleDoubleCoOp = new[]
        { ChartType.Single, ChartType.Double, ChartType.CoOp };

    // The sitting window D21 set: a play more than 15 minutes from a sitting's plays starts a new
    // one, and a sitting with nothing arriving for 15 minutes closes. A legacy mix carries it too,
    // though the plays endpoint never takes one.
    private static readonly TimeSpan QuarterHourSitting = TimeSpan.FromMinutes(15);

    private static readonly MixProfile Legacy = new(
        ScoringModel.Legacy, GradeLadder.Phoenix1, AwardSet.None, OfficialSite: null, Platform.Pad,
        HasLifebarModel: false, EveryLegacyType, MixArt.Flat, MixFeatures.None, QuarterHourSitting);

    private static readonly IReadOnlyDictionary<MixEnum, MixProfile> All = Build();

    public static MixProfile For(MixEnum mix)
    {
        return All.TryGetValue(mix, out var profile)
            ? profile
            : throw new ArgumentOutOfRangeException(nameof(mix), mix, "This mix has no profile");
    }

    private static IReadOnlyDictionary<MixEnum, MixProfile> Build()
    {
        var all = Enum.GetValues<MixEnum>().ToDictionary(m => m, _ => Legacy);
        all[MixEnum.Phoenix] = new MixProfile(
            ScoringModel.Phoenix, GradeLadder.Phoenix1, AwardSet.PhoenixPlates, OfficialSite: MixEnum.Phoenix,
            Platform.Pad, HasLifebarModel: true, SingleDoubleCoOp,
            new MixArt("Phoenix", null, HasHalfDoubleBubble: false), MixFeatures.Phoenix, QuarterHourSitting);
        // The Phoenix 2 stepball set carries the H. DOUBLE bubble piugame drew for the RISE MIX
        // channel (docs/design/rise.md D12); Phoenix 2 itself has no half-double charts to draw with it.
        all[MixEnum.Phoenix2] = new MixProfile(
            ScoringModel.Phoenix, GradeLadder.Phoenix2, AwardSet.PhoenixPlates, OfficialSite: MixEnum.Phoenix2,
            Platform.Pad, HasLifebarModel: true, SingleDoubleCoOp,
            new MixArt("Phoenix2", null, HasHalfDoubleBubble: true), MixFeatures.Phoenix, QuarterHourSitting);
        // Pump It Up RISE (docs/design/rise.md §3): Phoenix-scored on a keyboard, no site, no lifebar
        // the site models (D9), Rise's own nine-grade ladder and three marks, the Phoenix 2 stepballs
        // (D12) with Rise's own letters and marks, and none of the Phoenix-generation features until
        // phase 3 tunes them.
        all[MixEnum.Rise] = new MixProfile(
            ScoringModel.Phoenix, GradeLadder.Rise, AwardSet.RiseMarks, OfficialSite: null, Platform.Keyboard,
            HasLifebarModel: false, new[] { ChartType.Single, ChartType.HalfDouble },
            new MixArt("Phoenix2", "Rise", HasHalfDoubleBubble: true), MixFeatures.None, QuarterHourSitting);
        // The Arcade Station plays the Phoenix 2 charts as-is — Phoenix 2 grades, plates and art —
        // on the same keyboard.
        all[MixEnum.RiseArcade] = new MixProfile(
            ScoringModel.Phoenix, GradeLadder.Phoenix2, AwardSet.PhoenixPlates, OfficialSite: null,
            Platform.Keyboard, HasLifebarModel: false, new[] { ChartType.Single, ChartType.Double },
            new MixArt("Phoenix2", null, HasHalfDoubleBubble: true), MixFeatures.None, QuarterHourSitting);
        return all;
    }
}
