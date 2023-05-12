namespace ScoreTracker.Domain.Records;

public sealed record ScoreScreen(int Perfects, int Greats, int Goods, int Bads, int Misses, int MaxCombo)
{
    private int TotalCount => Perfects + Greats + Goods + Bads + Misses;

    public int CalculatePhoenixScore => TotalCount <= 0
        ? 0
        : (int)((.995 * (1.0 * Perfects + .6 * Greats + .2 * Goods + .1 * Bads) + .005 * MaxCombo) /
            (Perfects + Greats + Goods + Bads + Misses) * 1000000.0);

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
        >= 930000 => "AA+",
        >= 910000 => "AA",
        >= 890000 => "A+",
        >= 700000 => "A",
        _ => "B or Lower"
    };
}