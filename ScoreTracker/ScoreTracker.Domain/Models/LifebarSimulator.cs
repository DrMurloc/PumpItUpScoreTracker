using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

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

        if (_lifeMultiplier > .8) _lifeMultiplier = .8;

        if (CurrentLife < 0) CurrentLife = 0;

        if (CurrentLife > MaxLife) CurrentLife = MaxLife;
    }
}