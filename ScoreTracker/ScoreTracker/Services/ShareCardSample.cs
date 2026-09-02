using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The dialog's example (design doc §10): real jackets off the live list, each wearing a
///     scripted state, so every option in the dialog is visible in the example at once — a
///     Perfect Game for the color modes' glow, a pass in each Top 50 pool, a broken run, a To Do,
///     a pass carried from another mix, a bare chart. The owner had to fish across pages to see
///     how a download would come out; the example now guarantees the variety itself.
///     <para>
///         Nothing here reads the player's record. The values are stage props chosen so their
///         grades resolve honestly under either Phoenix mix; a legacy mix, which carries no
///         Phoenix number, keeps the states and drops the numbers.
///     </para>
/// </summary>
public static class ShareCardSample
{
    /// <summary>At most six tiles — the renderer ingests every jacket it is handed (design doc §6).</summary>
    public const int Size = 6;

    public static IReadOnlyList<ShareCardComposer.TileFacts> Facts(IReadOnlyList<Chart> charts, MixEnum mix,
        Func<Chart, IReadOnlyList<TierListChartCard.CardSkillChip>?> skills, Func<Chart, string?> bubble)
    {
        var scoring = !mix.UsesLegacyScoring();
        var facts = new List<ShareCardComposer.TileFacts>();
        for (var i = 0; i < Math.Min(Size, charts.Count); i++)
        {
            var chart = charts[i];
            facts.Add(i switch
            {
                0 => Fact(chart, mix, scoring, skills, bubble, 1_000_000, PhoenixPlate.PerfectGame,
                    passed: true, top50Combined: true),
                1 => Fact(chart, mix, scoring, skills, bubble, 957_320, PhoenixPlate.SuperbGame,
                    passed: true, top50Type: true, gain: 2.1, expected: 964_000),
                2 => Fact(chart, mix, scoring, skills, bubble, 921_540, PhoenixPlate.RoughGame,
                    broken: true, gain: 6.2, expected: 950_000),
                3 => Fact(chart, mix, scoring, skills, bubble, null, null, todo: true, gain: 12.4, expected: 964_000),
                4 => Fact(chart, mix, scoring, skills, bubble, null, null, other: true, gain: 8.9, expected: 941_500),
                _ => Fact(chart, mix, scoring, skills, bubble, null, null)
            });
        }

        return facts;
    }

    private static ShareCardComposer.TileFacts Fact(Chart chart, MixEnum mix, bool scoring,
        Func<Chart, IReadOnlyList<TierListChartCard.CardSkillChip>?> skills, Func<Chart, string?> bubble,
        int? score, PhoenixPlate? plate, bool passed = false, bool broken = false, bool todo = false,
        bool other = false, bool top50Type = false, bool top50Combined = false, double? gain = null,
        int? expected = null)
    {
        var phoenixScore = scoring && score is { } s ? PhoenixScore.From(s) : (PhoenixScore?)null;
        var phoenixPlate = scoring ? plate : null;
        return new ShareCardComposer.TileFacts(chart, phoenixScore, phoenixPlate,
            Passed: passed,
            Broken: broken,
            IsToDo: todo,
            PassedInOtherMix: other,
            InTop50Type: top50Type,
            InTop50Combined: top50Combined,
            gain,
            scoring && expected is { } e ? PhoenixScore.From(e) : null,
            passed ? ShareCardComposer.CurrentPumbility(chart, phoenixScore, phoenixPlate, false, mix) : null,
            skills(chart),
            bubble(chart));
    }
}
