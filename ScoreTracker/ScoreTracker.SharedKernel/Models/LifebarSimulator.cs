using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

public sealed class LifebarSimulator
{
    public int CurrentLife { get; private set; }
    public int MaxLife { get; }
    private double _lifeMultiplier;

    public LifebarSimulator(DifficultyLevel level, bool startAtFull = false)
    {
        if (level > 30)
            MaxLife = 3700;
        else
            MaxLife = 1000 + level * level * 3;

        CurrentLife = startAtFull ? MaxLife : 500;
        _lifeMultiplier = .1;
    }

    private LifebarSimulator(int maxLife, int currentLife, double lifeMultiplier)
    {
        MaxLife = maxLife;
        CurrentLife = currentLife;
        _lifeMultiplier = lifeMultiplier;
    }

    /// <summary>The life gain multiplier's ceiling.</summary>
    public const double MaxLifeMultiplier = .8;

    /// <summary>
    ///     A simulator resumed at a known state — for carrying a run across a level change,
    ///     where the bar rescales but the player hasn't played a note. Life clamps to the new
    ///     level's maximum. Replaying judgments to reach a target life instead would drift:
    ///     bads move in steps of 50 and overshoot.
    /// </summary>
    public static LifebarSimulator At(DifficultyLevel level, int currentLife, double lifeMultiplier)
    {
        var maxLife = new LifebarSimulator(level).MaxLife;
        return new LifebarSimulator(maxLife,
            Math.Clamp(currentLife, 0, maxLife),
            Math.Clamp(lifeMultiplier, 0, MaxLifeMultiplier));
    }

    /// <summary>
    ///     A detached copy at this exact state — including the life multiplier, which is the
    ///     half of the state nobody can see. Lets a caller ask "what would this judgment do?"
    ///     without committing to it.
    /// </summary>
    public LifebarSimulator Fork() => new(MaxLife, CurrentLife, _lifeMultiplier);

    /// <summary>
    ///     The life gain multiplier: near-zeroed by a miss, halved by a bad, rebuilt by clean
    ///     notes to a 0.8 cap. Exposed because the Life Calculator's whole point is showing it.
    /// </summary>
    public double LifeMultiplier => _lifeMultiplier;

    private readonly IDictionary<Judgment, int> JudgmentWeight = new Dictionary<Judgment, int>
    {
        { Judgment.Perfect, 12 },
        { Judgment.Great, 10 },
        { Judgment.Good, 0 },
        { Judgment.Bad, -50 },
        { Judgment.Miss, -500 }
    };


    public void ApplyJudgment(Judgment judgment)
    {
        var judgmentWeight = JudgmentWeight[judgment];

        switch (judgment)
        {
            case Judgment.Miss:
                CurrentLife += (int)(judgmentWeight * (CurrentLife > 1000 ? 1000 : CurrentLife) / 2000.0 - 20.0);
                _lifeMultiplier -= .7;
                break;
            case Judgment.Bad:
                CurrentLife += judgmentWeight;
                _lifeMultiplier -= .35;
                break;
            case Judgment.Good:
                //Goods do NOTHING for life
                break;
            case Judgment.Great:
                CurrentLife += (int)(judgmentWeight * _lifeMultiplier);
                _lifeMultiplier += .016;
                break;
            case Judgment.Perfect:
                CurrentLife += (int)(judgmentWeight * _lifeMultiplier);
                _lifeMultiplier += .020;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(judgment), judgment,
                    $"You somehow entered an alternate reality where {judgment} exists as a judgment");
        }

        if (_lifeMultiplier < 0) _lifeMultiplier = 0;

        if (_lifeMultiplier > MaxLifeMultiplier) _lifeMultiplier = MaxLifeMultiplier;

        if (CurrentLife < 0) CurrentLife = 0;

        if (CurrentLife > MaxLife) CurrentLife = MaxLife;
    }
}