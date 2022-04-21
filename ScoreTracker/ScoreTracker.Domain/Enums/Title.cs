using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Enums;

public enum Title
{
    Beginner,
    [DifficultyTitle(10, 11, 25)] IntermediateLv1,
    [DifficultyTitle(10, 11, 50)] IntermediateLv2,
    [DifficultyTitle(12, 13, 25)] IntermediateLv3,
    [DifficultyTitle(12, 13, 50)] IntermediateLv4,
    [DifficultyTitle(14, 15, 25)] IntermediateLv5,
    [DifficultyTitle(14, 15, 50)] IntermediateLv6,
    [DifficultyTitle(16, 17, 25)] IntermediateLv7,
    [DifficultyTitle(16, 17, 50)] IntermediateLv8,
    [DifficultyTitle(18, 19, 25)] IntermediateLv9,
    [DifficultyTitle(18, 19, 50)] IntermediateLv10,
    [DifficultyTitle(20, 25)] AdvancedLv1,
    [DifficultyTitle(20, 50)] AdvancedLv2,
    [DifficultyTitle(21, 25)] AdvancedLv3,
    [DifficultyTitle(21, 50)] AdvancedLv4,
    [DifficultyTitle(22, 20)] AdvancedLv5,
    [DifficultyTitle(22, 40)] AdvancedLv6,
    [DifficultyTitle(22, 60)] AdvancedLv7,
    [DifficultyTitle(23, 20)] AdvancedLv8,
    [DifficultyTitle(23, 35)] AdvancedLv9,
    [DifficultyTitle(23, 50)] AdvancedLv10,
    [DifficultyTitle(24, 30)] ExpertLv1,
    [DifficultyTitle(25, 15)] ExpertLv2,
    [DifficultyTitle(26, 7)] ExpertLv3,
    [DifficultyTitle(27, 3)] ExpertLv4,
    [DifficultyTitle(28, 1)] TheMaster,

    [SkillTitle("Street Show Down", ChartType.Single, 15)]
    TwistLv1,

    [SkillTitle("Final Audition 3", ChartType.Single, 16)]
    TwistLv2,

    [SkillTitle("U Got Me Rocking", ChartType.Single, 17)]
    TwistLv3,

    [SkillTitle("Final Audition", ChartType.Double, 18)]
    TwistLv4,

    [SkillTitle("Super Fantasy", ChartType.Single, 19)]
    TwistLv5,

    [SkillTitle("Witch Doctor #1", ChartType.Double, 20)]
    TwistLv6,

    [SkillTitle("Love is a Danger Zone", ChartType.Double, 21)]
    TwistLv7,

    [SkillTitle("Love is a Danger Zone", ChartType.Single, 22)]
    TwistLv8,

    [SkillTitle("Love is a Danger Zone (Cranky Mix)", ChartType.Double, 23)]
    TwistLv9,

    [SkillTitle("Bee", ChartType.Double, 24)]
    TwistLv10,
    TwistExpert,

    [SkillTitle("Final Audition", ChartType.Double, 15)]
    RunLv1,

    [SkillTitle("Super Fantasy", ChartType.Single, 16)]
    RunLv2,

    [SkillTitle("Pavane", ChartType.Single, 17)]
    RunLv3,

    [SkillTitle("Gothique Resonance", ChartType.Single, 18)]
    RunLv4,

    [SkillTitle("Napalm", ChartType.Single, 19)]
    RunLv5,

    [SkillTitle("Bee", ChartType.Double, 20)]
    RunLv6,

    [SkillTitle("Sarabande", ChartType.Double, 21)]
    RunLv7,

    [SkillTitle("Just Hold On (To Aall Fighters)", ChartType.Double, 22)]
    RunLv8,

    [SkillTitle("Final Audition Ep. 2-X", ChartType.Single, 23, LetterGrade.S)]
    RunLv9,

    [SkillTitle("Yog Sothoth", ChartType.Double, 24)]
    RunLv10,
    RunExpert,

    [SkillTitle("Vook", ChartType.Single, 15)]
    DrillLv1,

    [SkillTitle("Solitary 1.5", ChartType.Single, 16)]
    DrillLv2,

    [SkillTitle("Gun Rock", ChartType.Single, 17)]
    DrillLv3,

    [SkillTitle("Moonlight", ChartType.Single, 18)]
    DrillLv4,

    [SkillTitle("Vacuum", ChartType.Single, 19)]
    DrillLv5,

    [SkillTitle("Overblow", ChartType.Single, 20)]
    DrillLv6,

    [SkillTitle("Sorceress Elise", ChartType.Single, 21)]
    DrillLv7,

    [SkillTitle("Rock the House", ChartType.Double, 22)]
    DrillLv8,

    [SkillTitle("Witch Doctor", ChartType.Double, 23)]
    DrillLv9,

    [SkillTitle("Wi-Ex-Doc-Va", ChartType.Double, 24)]
    DrillLv10,
    DrillExpert,

    [SkillTitle("Yeo Rae A", ChartType.Single, 13)]
    GimmickLv1,

    [SkillTitle("Bad Apple", ChartType.Single, 15)]
    GimmickLv2,

    [SkillTitle("Love Scenario", ChartType.Single, 17)]
    GimmickLv3,

    [SkillTitle("Come to Me", ChartType.Single, 17)]
    GimmickLv4,

    [SkillTitle("Rock the House (Short Cut)", ChartType.Single, 18)]
    GimmickLv5,

    [SkillTitle("Miss S' Story", ChartType.Single, 19)]
    GimmickLv6,

    [SkillTitle("Nakakapagpabagabag", ChartType.Single, 19)]
    GimmickLv7,

    [SkillTitle("Twist of Fate", ChartType.Single, 19)]
    GimmickLv8,

    [SkillTitle("Everybody Got 2 Know", ChartType.Single, 19)]
    GimmickLv9,

    [SkillTitle("86", ChartType.Single, 20)]
    GimmickLv10,
    GimmickExpert,

    [SkillTitle("Trashy Innocence", ChartType.Double, 16)]
    HalfLv1,

    [SkillTitle("Butterfly", ChartType.Double, 17)]
    HalfLv2,

    [SkillTitle("Shub Niggurath", ChartType.Double, 18)]
    HalfLv3,

    [SkillTitle("Super Fantasy", ChartType.Double, 18)]
    HalfLv4,

    [SkillTitle("Phantom", ChartType.Double, 19)]
    HalfLv5,

    [SkillTitle("Utsushiyo no Kaze", ChartType.Double, 20)]
    HalfLv6,

    [SkillTitle("Witch Doctor #1", ChartType.Double, 21)]
    HalfLv7,

    [SkillTitle("Bad Apple (Full Song)", ChartType.Double, 22)]
    HalfLv8,

    [SkillTitle("Love is a Danger Zone (Try to B.P.M)", ChartType.Double, 23)]
    HalfLv9,

    [SkillTitle("Imprinting", ChartType.Double, 24)]
    HalfLv10,
    HalfExpert,
    Specialist,
    ExcFollower,
    NimgoFollower,
    WindforceFollower,
    BMEFollower,
    ConradFollower,
    OsingFollower,
    FefemzFollower,
    AeviluxFollower,
    SphamFollower,
    SunnyFollower,
    MaxFollower,
    AtasFollower,
    ShkFollower,
    NatoFollower,
    DoinFollower,
    PoryFollower,
    KienFollower,
    HyunFollower,
    QureeFollower,
    ApplesodaFollower,
    DMAshuraFollower
}

public sealed class SkillTitle : Attribute
{
    public SkillTitle(string songName, ChartType chartType, int level, LetterGrade letterGradeRequirement)
    {
        SongName = songName;
        LetterGradeRequirement = letterGradeRequirement;
        ChartType = chartType;
        Level = level;
    }

    public SkillTitle(string songName, ChartType chartType, int level) : this(songName, chartType, level,
        LetterGrade.SS)
    {
    }

    public LetterGrade LetterGradeRequirement { get; }
    public Name SongName { get; }
    public ChartType ChartType { get; }
    public DifficultyLevel Level { get; }
}

public sealed class DifficultyTitle : Attribute
{
    public DifficultyTitle(int minLevel, int maxLevel, int count)
    {
        MinLevel = minLevel;
        MaxLevel = maxLevel;
        Count = count;
    }

    public DifficultyTitle(int level, int count) : this(level, level, count)
    {
    }

    public DifficultyLevel MinLevel { get; }
    public DifficultyLevel MaxLevel { get; }
    public int Count { get; }
}

public sealed class TitleName : Attribute
{
    public TitleName(string name)
    {
        Name = name;
    }

    public string Name { get; }
}