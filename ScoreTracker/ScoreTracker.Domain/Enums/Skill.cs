﻿using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum Skill
{
    [Name("Long Runs")] [Description("Runs that last many measures")] [SkillCategory(SkillCategory.Stamina)]
    LongRuns,

    [Name("Fast Runs")]
    [Description("Runs that sustain a high footspeed (typically 200+ BPM) for a long time")]
    [SkillCategory(SkillCategory.Stamina)]
    FastRuns,

    [Name("Fast Bursts")]
    [Description("High footspeed, typically back to back, shorter patterns")]
    [SkillCategory(SkillCategory.Stamina)]
    FastBursts,

    [Name("Flying")]
    [Description("Doubles patterns that make you move rapidly from one pad to another")]
    [SkillCategory(SkillCategory.Stamina)]
    Flying,

    [Name("Teleporting")]
    [Description(
        "Doubles patterns that make you move instantly from one pad to another, typically as a jumping motion")]
    [SkillCategory(SkillCategory.Stamina)]
    Teleporting,

    [Name("Twisty Runs")]
    [Description("Runs that have a high amount of twists mixed in")]
    [SkillCategory(SkillCategory.Twist)]
    TwistyRuns,

    [Name("Sideways Runs")]
    [Description("Runs that keep you turned left or right for long durations")]
    [SkillCategory(SkillCategory.Twist)]
    SidewaysRuns,

    [Name("Deep Twists")] [Description("Twists that make you turn backwards")] [SkillCategory(SkillCategory.Twist)]
    DeepTwists,

    [Name("Outer Pad Turns")]
    [Description("Doubles Turns that occur at the far outer portions of the pads")]
    [SkillCategory(SkillCategory.Twist)]
    OuterPadTurns,

    [Name("Side Pivots")]
    [Description("Repeated turns on the three left or right arrows on a pad")]
    [SkillCategory(SkillCategory.Twist)]
    SidePivots,

    [Name("Top/Bottom Pivots")]
    [Description("Turns on the three bottom or top arrows on a pad")]
    [SkillCategory(SkillCategory.Twist)]
    TopBottomPivots,

    [Name("M Runs")]
    [Description("Patterns on one pad that use all 5 arrows going from one side to another")]
    [SkillCategory(SkillCategory.Twist)]
    MRuns,

    [Name("Staircases")]
    [Description("Doubles pattern that lines up arrows in a staircase pattern from side to side")]
    [SkillCategory(SkillCategory.Twist)]
    Staircases,

    [Name("Drills")] [Description("Repeated alternated steps")] [SkillCategory(SkillCategory.Tech)]
    Drills,

    [Name("Twisty Drills")]
    [Description("Drills that have you turn while drilling")]
    [SkillCategory(SkillCategory.Tech, SkillCategory.Twist)]
    TwistyDrills,

    [Name("Skips")]
    [Description("A step followed immediately by another followed by a pause")]
    [SkillCategory(SkillCategory.Tech)]
    Skips,

    [Name("Gallops")]
    [Description("Three steps rapidly in a row, often back to back with breaks between")]
    [SkillCategory(SkillCategory.Tech)]
    Gallops,

    [Name("Twisty Gallops")]
    [Description("Gallops that have you twist")]
    [SkillCategory(SkillCategory.Tech, SkillCategory.Twist)]
    TwistingGallops,

    [Name("Half Double Runs")] [Description("Doubles Runs that only use the center 6 arrows")] [SkillCategory]
    HalfDoubleRuns,

    [Name("Quarter Double Runs")] [Description("Doubles Runs that only use the center 4 arrows")] [SkillCategory]
    QuarterDoubleRuns,

    [Name("Top/Bottom Half Double Runs")]
    [Description("Doubles Runs that only use the top or bottom 6 arrows")]
    [SkillCategory]
    TopBottomHalfDoubleRuns,

    [Name("Splits")]
    [Description("Doubles patterns or jumps that utilize opposing side arrows on both pads at once")]
    [SkillCategory]
    Splits,

    [Name("Quads")] [Description("Jumps utilizing 4 arrows at once")] [SkillCategory(SkillCategory.Tech)]
    Quads,

    [Name("Bracket Jump Tech")]
    [Description("Jumps, often repeated, utilizing 3 or more arrows at once")]
    [SkillCategory(SkillCategory.Tech)]
    BracketJumpTech,

    [Name("Bracket Drills")]
    [Description("Drills that utilize brackets for one or both feet")]
    [SkillCategory(SkillCategory.Tech)]
    BracketDrills,

    [Name("Bracket Runs")]
    [Description("Runs that utilize a significant amount of brackets")]
    [SkillCategory(SkillCategory.Tech)]
    BracketRuns,

    [Name("Rolling Brackets")]
    [Description("Bracket steps that roll your foot from toe to heel or heel to toe")]
    [SkillCategory(SkillCategory.Tech)]
    RollingBrackets,

    [Name("Jump Spam")] [Description("Many jumps back to back")] [SkillCategory(SkillCategory.Tech)]
    JumpSpam,

    [Name("Jump Jacks")]
    [Description("The same jump multiple times in a row at a high speed")]
    [SkillCategory(SkillCategory.Tech)]
    JumpJacks,

    [Name("Spins")] [Description("Patterns that make you turn a full 360")] [SkillCategory(SkillCategory.Tech)]
    Spins,

    [Name("Jacks/Footswitches")]
    [Description("Patterns that repeat the samme note over and over again")]
    [SkillCategory(SkillCategory.Tech)]
    Jacks,

    [Name("Visual Gimmicks")]
    [Description("Warps, Hidden Arrows, Flashes, etc. that add difficulty to a chart")]
    [SkillCategory(SkillCategory.Gimmick)]
    VisualGimmicks,

    [Name("Stop Gimmicks")]
    [Description("Arrows that stop and go to the rhythm of the music")]
    [SkillCategory(SkillCategory.Gimmick)]
    StopGimmicks,

    [Name("BPM Changes")]
    [Description("Slowdowns or Speedups that contribute to scoring or passing difficulty")]
    [SkillCategory(SkillCategory.Gimmick)]
    BPMChanges,


    [Name("What?")] [Description("Chart contains uniquely crazy patterns or gimmicks")] [SkillCategory]
    What,

    [Name("Anchor Runs")]
    [Description("Runs where one foot remains on the same arrow for long segments")]
    [SkillCategory(SkillCategory.Tech)]
    AnchorRuns,

    [Name("Cross-overs")]
    [Description("Turning patterns that move your foot in 180 degree-ish patterns across your other foot")]
    [SkillCategory(SkillCategory.Twist)]
    CrossOvers,

    [Name("Mid-Sized Runs")]
    [Description("Many runs that last a few measures but not terribly long")]
    [SkillCategory(SkillCategory.Stamina)]
    MediumRuns,

    [Name("High Movement Runs")]
    [Description("Doubles Runs that move you around a lot or have your legs extend far out often")]
    [SkillCategory(SkillCategory.Stamina)]
    HighMovement,

    [Name("Double Steps")]
    [Description("Patterns that force you to double step, typically with one foot on a hold")]
    [SkillCategory(SkillCategory.Tech)]
    DoubleSteps,

    [Name("Anchor Holds")]
    [Description(
        "Patterns that have you hold with a heal or toe while hitting arrows with the other side of your foot")]
    [SkillCategory(SkillCategory.Tech)]
    AnchorHolds,

    [Name("Shuffles")]
    [Description(
        "Patterns that make you slide your foot along a hold without letting go to change which arrows you are hitting")]
    [SkillCategory(SkillCategory.Tech)]
    Shuffles,

    [Name("Yog Walks")]
    [Description("Doubles runs that move you from side to side in a steady (not rapid) continuous stream")]
    [SkillCategory(SkillCategory.Tech)]
    YogWalks,

    [Name("Crab Walks")]
    [Description("Doubles runs that move sideways while maintaining a wide stance")]
    [SkillCategory(SkillCategory.Tech)]
    CrabWalks
}

public sealed class NameAttribute : Attribute
{
    public string Name { get; }

    public NameAttribute(string name)
    {
        Name = name;
    }
}

public sealed class SkillCategoryAttribute : Attribute
{
    public SkillCategory[] Categories { get; }

    public SkillCategoryAttribute(params SkillCategory[] categories)
    {
        Categories = categories;
    }
}

public static class SkillHelpers
{
    private static readonly IDictionary<Skill, SkillCategory[]> Categories = Enum.GetValues<Skill>()
        .ToDictionary(c => c, c => typeof(Skill).GetField(c.ToString())
            ?.GetCustomAttribute<SkillCategoryAttribute>()
            ?.Categories ?? Array.Empty<SkillCategory>());

    private static readonly IDictionary<Skill, string> Colors =
        Categories.ToDictionary(kv => kv.Key, kv => kv.Value.Cast<SkillCategory?>().FirstOrDefault()?.GetColor()??"#333333");

    private static readonly IDictionary<Skill, string> Names = Enum.GetValues<Skill>()
        .ToDictionary(c => c, c => typeof(Skill).GetField(c.ToString())
            ?.GetCustomAttribute<NameAttribute>()
            ?.Name ?? "");

    private static readonly IDictionary<Skill, string> Descriptions = Enum.GetValues<Skill>()
        .ToDictionary(c => c, c => typeof(Skill).GetField(c.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? "");

    public static string GetDescription(this Skill skill)
    {
        return Descriptions[skill];
    }

    public static string GetColor(this Skill skill)
    {
        return Colors?[skill]??"";
    }

    public static string GetName(this Skill skill)
    {
        return Names[skill];
    }
}