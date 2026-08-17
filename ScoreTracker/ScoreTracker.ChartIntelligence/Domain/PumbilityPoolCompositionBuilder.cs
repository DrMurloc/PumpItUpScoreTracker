using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     One pooled chart as the composition sees it: its printed level, the grade its score earned in
///     the mix's own cutoffs, and the exact three-way split of what it contributes.
/// </summary>
internal readonly record struct PooledChart(int Level, PhoenixLetterGrade Grade, ScoreContribution Parts);

/// <summary>
///     Accumulates full merged pools into per-band sums (docs/design/pumbility-calculator.md D9/D10).
///     Pure: the sweep feeds it one pool at a time and asks for the record at the end. Every band the
///     mix defines is present in the result — thin and empty ones too — so the page can name a rung
///     it cannot yet draw. The split is exact, not modelled: it sums the same
///     <see cref="ScoringConfiguration.Decompose" /> parts the PUMBILITY page shows, so the two agree.
/// </summary>
internal sealed class PumbilityPoolCompositionBuilder
{
    private readonly Dictionary<string, Accumulator> _bands;
    private readonly MixEnum _mix;
    private int _pools;

    public PumbilityPoolCompositionBuilder(MixEnum mix)
    {
        _mix = mix;
        _bands = PumbilityPoolBands.For(mix).ToDictionary(b => b.Key, b => new Accumulator(b));
    }

    /// <summary>
    ///     Adds one full pool. <paramref name="charts" /> is the pool's fifty; the band is chosen by
    ///     the sum of their contributions, which is the pool total the titles use.
    /// </summary>
    public void Add(IReadOnlyCollection<PooledChart> charts)
    {
        var total = charts.Sum(c => c.Parts.Base + c.Parts.FromGrade + c.Parts.FromPlate);
        var band = PumbilityPoolBands.BandFor(_mix, total);
        if (band == null) return;
        _pools++;
        _bands[band.Key].Add(charts);
    }

    public PumbilityPoolCompositionRecord Build(DateTimeOffset computedAt)
    {
        return new PumbilityPoolCompositionRecord(_mix, computedAt, _pools,
            PumbilityPoolBands.For(_mix).Select(b => _bands[b.Key].ToRecord()).ToArray());
    }

    private sealed class Accumulator
    {
        private readonly PumbilityPoolBand _band;
        private readonly Dictionary<PhoenixLetterGrade, int> _grades = new();
        private int _charts;
        private double _levelPart;
        private double _levelSum;
        private double _platePart;
        private int _players;
        private double _scorePart;

        public Accumulator(PumbilityPoolBand band)
        {
            _band = band;
        }

        public void Add(IReadOnlyCollection<PooledChart> charts)
        {
            _players++;
            foreach (var chart in charts)
            {
                _charts++;
                _levelSum += chart.Level;
                _levelPart += chart.Parts.Base;
                _scorePart += chart.Parts.FromGrade;
                _platePart += chart.Parts.FromPlate;
                _grades[chart.Grade] = _grades.GetValueOrDefault(chart.Grade) + 1;
            }
        }

        public PumbilityPoolBandRecord ToRecord()
        {
            return new PumbilityPoolBandRecord(_band.Key, _band.Title, _band.Floor, _band.Ceiling, _players, _charts,
                _levelSum, _levelPart, _scorePart, _platePart,
                new Dictionary<PhoenixLetterGrade, int>(_grades));
        }
    }
}
