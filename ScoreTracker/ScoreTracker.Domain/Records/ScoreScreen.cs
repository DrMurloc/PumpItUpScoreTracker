using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

public sealed record ScoreScreen(StepCount Perfects, StepCount Greats, StepCount Goods, StepCount Bads,
    StepCount Misses, StepCount MaxCombo, double? Calories = null)
{
    private static readonly Random Random = new(1949);
    public int TotalCount => Perfects + Greats + Goods + Bads + Misses;

    public PhoenixScore CalculatePhoenixScore => !IsValid
        ? 0
        : (int)Math.Ceiling((.995 * (1.0 * Perfects + .6 * Greats + .2 * Goods + .1 * Bads) + .005 * MaxCombo) /
            (Perfects + Greats + Goods + Bads + Misses) * 1000000.0);

    public int GreatLoss => (int)(.995 * .4 * Greats / TotalCount * 1000000.0);
    public int GoodLoss => (int)(.995 * .8 * Goods / TotalCount * 1000000.0);
    public int BadLoss => (int)(.995 * .9 * Bads / TotalCount * 1000000.0);
    public int MissLoss => (int)(.995 * Misses / TotalCount * 1000000.0);
    public int ComboLoss => (int)(.005 * (TotalCount - MaxCombo) / TotalCount * 1000000.0);

    private static readonly IDictionary<int, int> EstimatedNoteCountThresholds =
        new Dictionary<int, int>
        {
            { 57, 1 },
            { 123, 2 },
            { 174, 3 },
            { 236, 4 },
            { 309, 5 },
            { 372, 6 },
            { 468, 7 },
            { 524, 8 },
            { 555, 9 },
            { 572, 10 },
            { 597, 11 },
            { 700, 12 }
        };

    private double CaloriesPerStep => TotalCount > 700
        ? .0621
        : .035 + .0023 * EstimatedNoteCountThresholds.OrderBy(kv => kv.Key).First(kv => kv.Key >= TotalCount)
            .Value;

    public double? EstimatedSteps => Calories == null ? null : Calories / CaloriesPerStep;

    public PhoenixPlate PlateText
    {
        get
        {
            if (Greats == 0 && Goods == 0 && Bads == 0 && Misses == 0) return PhoenixPlate.PerfectGame;
            if (Goods == 0 && Bads == 0 && Misses == 0) return PhoenixPlate.UltimateGame;
            if (Bads == 0 && Misses == 0) return PhoenixPlate.ExtremeGame;
            return (int)Misses switch
            {
                0 => PhoenixPlate.SuperbGame,
                <= 5 => PhoenixPlate.MarvelousGame,
                <= 10 => PhoenixPlate.TalentedGame,
                <= 20 => PhoenixPlate.FairGame,
                _ => PhoenixPlate.RoughGame
            };
        }
    }

    public PhoenixLetterGrade LetterGrade => CalculatePhoenixScore.LetterGrade;

    public bool IsValid => TotalCount is < 10000 and > 0 &&
                           TotalCount >= MaxCombo
                           && MaxCombo >= 0;

    public string NextLetterGrade()
    {
        if (LetterGrade == PhoenixLetterGrade.SSSPlus) return "This is best grade possible!";
        if (!IsValid) return "This Score is Invalid";
        var next = IterateWithWeightedRandom(this);
        while (next.LetterGrade == LetterGrade) next = IterateWithWeightedRandom(next);

        var misses = Misses - next.Misses;
        var perfects = next.Perfects - Perfects;
        var bads = Bads - next.Bads;
        var goods = Goods - next.Goods;
        var greats = Greats - next.Greats;
        var comboGain = misses + bads + goods;
        var result = $"Get a {next.LetterGrade.GetName()} with";
        if (perfects > 0) result += $" {perfects} more Perfects,";

        if (misses > 0) result += $" {misses} fewer Misses,";

        if (bads > 0) result += $" {bads} fewer Bads,";

        if (goods > 0) result += $" {goods} fewer Goods,";

        if (greats > 0) result += $" {greats} fewer Greats,";

        result = result[..^1];

        if (comboGain > 0) result += $" and {comboGain} more Combo!";

        return result;
    }

    private static ScoreScreen IterateWithWeightedRandom(ScoreScreen previous)
    {
        var total = previous.Misses + previous.Bads + previous.Goods + previous.Greats;

        if (total <= 0) return previous with { MaxCombo = +1 };

        var next = Random.Next(total) + 1;
        if (next > total - previous.Greats)
            return previous with
            {
                Greats = previous.Greats - 1,
                Perfects = previous.Perfects + 1
            };
        if (next > total - previous.Greats - previous.Goods)
            return previous with
            {
                Goods = previous.Goods - 1,
                Perfects = previous.Perfects + 1,
                MaxCombo = previous.MaxCombo + 1
            };
        if (next > total - previous.Greats - previous.Goods - previous.Bads)
            return previous with
            {
                Bads = previous.Bads - 1,
                Perfects = previous.Perfects + 1,
                MaxCombo = previous.MaxCombo + 1
            };
        return previous with
        {
            Misses = previous.Misses - 1,
            Perfects = previous.Perfects + 1,
            MaxCombo = previous.MaxCombo + 1
        };
    }
}