using System.ComponentModel;
using System.Reflection;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Enums;

public enum PhoenixLetterGrade
{
    [ScoreRange(0, 449999)] [Modifier(.4)] F,

    [ScoreRange(450000, 549999)] [Modifier(.5)]
    D,

    [ScoreRange(550000, 649999)] [Modifier(.6)]
    C,

    [ScoreRange(650000, 749999)] [Modifier(.7)]
    B,

    [ScoreRange(750000, 824999)] [Modifier(.8)]
    A,

    [ScoreRange(825000, 899999)] [Modifier(.9)] [Description("A+")]
    APlus,

    [ScoreRange(900000, 924999)] [Modifier(1)]
    AA,

    [ScoreRange(925000, 949999)] [Modifier(1.05)] [Description("AA+")]
    AAPlus,

    [ScoreRange(950000, 959999)] [Modifier(1.10)]
    AAA,

    [ScoreRange(960000, 969999)] [Modifier(1.15)] [Description("AAA+")]
    AAAPlus,

    [ScoreRange(970000, 974999)] [Modifier(1.20)]
    S,

    [ScoreRange(975000, 979999)] [Modifier(1.26)] [Description("S+")]
    SPlus,

    [ScoreRange(980000, 984999)] [Modifier(1.32)]
    SS,

    [ScoreRange(985000, 989999)] [Modifier(1.38)] [Description("SS+")]
    SSPlus,

    [ScoreRange(990000, 994999)] [Modifier(1.44)]
    SSS,

    [ScoreRange(995000, 1000000)] [Modifier(1.50)] [Description("SSS+")]
    SSSPlus
}

[ExcludeFromCodeCoverage]
internal sealed class ScoreRangeAttribute : Attribute
{
    public ScoreRangeAttribute(int minimumScore, int maximumScore)
    {
        MinimumScore = minimumScore;
        MaximumScore = maximumScore;
    }

    public PhoenixScore MinimumScore { get; }
    public PhoenixScore MaximumScore { get; }
}

[ExcludeFromCodeCoverage]
internal sealed class ModifierAttribute : Attribute
{
    public ModifierAttribute(double modifier)
    {
        Modifier = modifier;
    }

    public double Modifier { get; }
}

[ExcludeFromCodeCoverage]
public static class PhoenixLetterGradeHelperMethods
{
    private static readonly IDictionary<PhoenixLetterGrade, ScoreRangeAttribute> CachedRanges = Enum
        .GetValues<PhoenixLetterGrade>()
        .ToDictionary(g => g,
            g => typeof(PhoenixLetterGrade).GetField(g.ToString())?.GetCustomAttribute<ScoreRangeAttribute>() ??
                 throw new Exception($"Score Range not set up for {g}"));

    // Phoenix 2 rebalanced the sub-AAA grade cutoffs upward — verified from live piugame.com
    // boards 2026-07-18 (see ScoreTracker.Tests.Integration/LiveSite/Phoenix2GradeThresholdReconTests):
    // A 800k, A+ 900k, AA 920k, AA+ 940k; AAA (950k) and everything above are identical to Phoenix.
    // A-tier through AAA are crawl-verified. The A and B floors are BRACKETED by live board rows
    // (2026-07-19, the sub800k probe fact in the same recon class reading grade art off the only
    // eight sub-800k rows on any board): 799,815 = B vs 804,414 = A pins the A floor to 800k
    // within a 4.6k window, and 690,647 = C vs 707,042 = B pins the B floor to 700k within a
    // 17k window. C and D floors are owner-ratified working values (2026-07-19: hold
    // B 700k / C 600k / D 500k until proven wrong) — nothing below 690,647 exists on any
    // board, so they are unobservable from crawling. F stays 0 as the lookup's catch-all:
    // with D at 500k there is no band boundary below it for an F floor to express. Only the
    // score→grade cutoffs differ per mix; grade identity, modifiers and names are shared, so
    // only the floors table is overridden here.
    private static readonly IReadOnlyDictionary<PhoenixLetterGrade, int> Phoenix2Floors =
        new Dictionary<PhoenixLetterGrade, int>
        {
            [PhoenixLetterGrade.F] = 0,
            [PhoenixLetterGrade.D] = 500000,
            [PhoenixLetterGrade.C] = 600000,
            [PhoenixLetterGrade.B] = 700000,
            [PhoenixLetterGrade.A] = 800000,
            [PhoenixLetterGrade.APlus] = 900000,
            [PhoenixLetterGrade.AA] = 920000,
            [PhoenixLetterGrade.AAPlus] = 940000,
            [PhoenixLetterGrade.AAA] = 950000,
            [PhoenixLetterGrade.AAAPlus] = 960000,
            [PhoenixLetterGrade.S] = 970000,
            [PhoenixLetterGrade.SPlus] = 975000,
            [PhoenixLetterGrade.SS] = 980000,
            [PhoenixLetterGrade.SSPlus] = 985000,
            [PhoenixLetterGrade.SSS] = 990000,
            [PhoenixLetterGrade.SSSPlus] = 995000
        };

    // Grades highest-floor-first, per mix, so a score resolves to the first grade it clears.
    private static readonly IReadOnlyList<(PhoenixLetterGrade Grade, int Floor)> Phoenix1FloorsDescending =
        Enum.GetValues<PhoenixLetterGrade>()
            .Select(g => (g, (int)CachedRanges[g].MinimumScore))
            .OrderByDescending(x => x.Item2).ToArray();

    private static readonly IReadOnlyList<(PhoenixLetterGrade Grade, int Floor)> Phoenix2FloorsDescending =
        Phoenix2Floors.Select(kv => (kv.Key, kv.Value)).OrderByDescending(x => x.Item2).ToArray();

    private static readonly IDictionary<string, PhoenixLetterGrade> Parser =
        Enum.GetValues<PhoenixLetterGrade>().ToDictionary(e => e.GetName());

    public static PhoenixScore GetMinimumScore(this PhoenixLetterGrade letterGrade)
    {
        return CachedRanges[letterGrade].MinimumScore;
    }

    public static PhoenixScore GetMaximumScore(this PhoenixLetterGrade letterGrade)
    {
        return CachedRanges[letterGrade].MaximumScore;
    }

    /// <summary>
    ///     The minimum score that earns <paramref name="letterGrade" /> in the given mix. Phoenix 2
    ///     shifted the sub-AAA cutoffs (see <see cref="Phoenix2Floors" />); every other mix uses the
    ///     original Phoenix table.
    /// </summary>
    public static PhoenixScore GetMinimumScoreFor(this PhoenixLetterGrade letterGrade, MixEnum mix)
    {
        return mix == MixEnum.Phoenix2 ? Phoenix2Floors[letterGrade] : CachedRanges[letterGrade].MinimumScore;
    }

    /// <summary>
    ///     The letter grade a raw score earns in the given mix — the single mix-aware entry point
    ///     for score→grade resolution. Callers that know which mix a record belongs to must use this
    ///     rather than the parameterless <see cref="PhoenixScore.LetterGrade" /> (which is the Phoenix
    ///     default), or a Phoenix 2 score in the 800k–950k band grades one rung too high.
    /// </summary>
    public static PhoenixLetterGrade LetterGradeFor(this PhoenixScore score, MixEnum mix)
    {
        var floors = mix == MixEnum.Phoenix2 ? Phoenix2FloorsDescending : Phoenix1FloorsDescending;
        return floors.First(f => (int)score >= f.Floor).Grade;
    }

    public static double GetModifier(this PhoenixLetterGrade enumValue)
    {
        return typeof(PhoenixLetterGrade).GetField(enumValue.ToString())?.GetCustomAttribute<ModifierAttribute>()?
            .Modifier ?? throw new Exception($"Phoenix letter grade {enumValue} is missing an attribute");
    }

    public static PhoenixLetterGrade? TryParse(string? value)
    {
        return value == null ? null : Parser.ContainsKey(value) ? Parser[value] : null;
    }

    public static string GetName(this PhoenixLetterGrade enumValue)
    {
        return typeof(PhoenixLetterGrade).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }
}
