namespace ScoreTracker.Domain.Records;

public sealed record ScoreScreen(int Perfects, int Greats, int Goods, int Bads, int Misses, int MaxCombo)
{
    private static readonly Random _random = new(1949);
    private int TotalCount => Perfects + Greats + Goods + Bads + Misses;

    public int CalculatePhoenixScore => TotalCount <= 0
        ? 0
        : (int)((.995 * (1.0 * Perfects + .6 * Greats + .2 * Goods + .1 * Bads) + .005 * MaxCombo) /
            (Perfects + Greats + Goods + Bads + Misses) * 1000000.0);

    public string PlateText
    {
        get
        {
            if (Greats == 0 && Goods == 0 && Bads == 0 && Misses == 0) return "Perfect Game";
            if (Goods == 0 && Bads == 0 && Misses == 0) return "Ultimate Game";
            if (Bads == 0 && Misses == 0) return "Extreme Game";
            return Misses switch
            {
                0 => "Superb Game",
                <= 5 => "Marvelous Game",
                <= 10 => "Talented Game",
                <= 20 => "Fair Game",
                _ => "Rough Game"
            };
        }
    }

    public string LetterGrade => CalculatePhoenixScore switch
    {
        >= 995000 => "SSS+",
        >= 990000 => "SSS",
        >= 985000 => "SS+",
        >= 980000 => "SS",
        >= 975000 => "S+",
        >= 970000 => "S",
        >= 960000 => "AAA+",
        >= 950000 => "AAA",
        >= 925000 => "AA+",
        >= 900000 => "AA",
        >= 800000 => "A+",
        >= 750000 => "A",
        >= 700000 => "B",
        _ => "C"
    };

    public bool IsValid => Perfects >= 0 && Greats >= 0 && Goods >= 0 && Bads >= 0 && Misses >= 0 &&
                           Perfects + Misses + Goods + Greats + Bads > 0 &&
                           Perfects + Misses + Goods + Greats + Bads >= MaxCombo
                           && MaxCombo >= 0;

    public string NextLetterGrade()
    {
        if (LetterGrade == "SSS+") return "This is best grade possible!";
        if (!IsValid) return "This Score is Invalid";
        var next = IterateWithWeightedRandom(this);
        while (next.LetterGrade == LetterGrade) next = IterateWithWeightedRandom(next);

        var misses = Misses - next.Misses;
        var perfects = next.Perfects - Perfects;
        var bads = Bads - next.Bads;
        var goods = Goods - next.Goods;
        var greats = Greats - next.Greats;
        var comboGain = misses + bads + goods;
        var result = $"Get a {next.LetterGrade} with";
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

        var next = _random.Next(total) + 1;
        if (next > total - previous.Greats)
            return previous with
            {
                Greats = previous.Greats - 1, Perfects = previous.Perfects + 1
            };
        if (next > total - previous.Greats - previous.Goods)
            return previous with
            {
                Goods = previous.Goods - 1, Perfects = previous.Perfects + 1, MaxCombo = previous.MaxCombo + 1
            };
        if (next > total - previous.Greats - previous.Goods - previous.Bads)
            return previous with
            {
                Bads = previous.Bads - 1, Perfects = previous.Perfects + 1, MaxCombo = previous.MaxCombo + 1
            };
        return previous with
        {
            Misses = previous.Misses - 1, Perfects = previous.Perfects + 1, MaxCombo = previous.MaxCombo + 1
        };
    }
}