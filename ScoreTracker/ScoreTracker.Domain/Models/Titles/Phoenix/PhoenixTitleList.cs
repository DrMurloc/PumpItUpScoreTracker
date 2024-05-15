﻿using System.Collections.Immutable;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public static class PhoenixTitleList
{
    private static readonly PhoenixTitle[] Titles =
    {
        new PhoenixBasicTitle("Beginner", "Default title"),
        new PhoenixDifficultyTitle("Intermediate Lv. 1", 10, 2000),
        new PhoenixDifficultyTitle("Intermediate Lv. 2", 11, 2200),
        new PhoenixDifficultyTitle("Intermediate Lv. 3", 12, 2600),
        new PhoenixDifficultyTitle("Intermediate Lv. 4", 13, 3200),
        new PhoenixDifficultyTitle("Intermediate Lv. 5", 14, 4000),
        new PhoenixDifficultyTitle("Intermediate Lv. 6", 15, 5000),
        new PhoenixDifficultyTitle("Intermediate Lv. 7", 16, 6200),
        new PhoenixDifficultyTitle("Intermediate Lv. 8", 17, 7600),
        new PhoenixDifficultyTitle("Intermediate Lv. 9", 18, 9200),
        new PhoenixDifficultyTitle("Intermediate Lv. 10", 19, 11000),
        new PhoenixDifficultyTitle("Advanced Lv. 1", 20, 13000),
        new PhoenixDifficultyTitle("Advanced Lv. 2", 20, 26000),
        new PhoenixDifficultyTitle("Advanced Lv. 3", 20, 39000),
        new PhoenixDifficultyTitle("Advanced Lv. 4", 21, 15000),
        new PhoenixDifficultyTitle("Advanced Lv. 5", 21, 30000),
        new PhoenixDifficultyTitle("Advanced Lv. 6", 21, 45000),
        new PhoenixDifficultyTitle("Advanced Lv. 7", 22, 17500),
        new PhoenixDifficultyTitle("Advanced Lv. 8", 22, 35000),
        new PhoenixDifficultyTitle("Advanced Lv. 9", 22, 52500),
        new PhoenixDifficultyTitle("Advanced Lv. 10", 22, 70000),
        new PhoenixDifficultyTitle("Expert Lv. 1", 23, 40000),
        new PhoenixDifficultyTitle("Expert Lv. 2", 23, 80000),
        new PhoenixDifficultyTitle("Expert Lv. 3", 24, 30000),
        new PhoenixDifficultyTitle("Expert Lv. 4", 24, 60000),
        new PhoenixDifficultyTitle("Expert Lv. 5", 25, 20000),
        new PhoenixDifficultyTitle("Expert Lv. 6", 25, 40000),
        new PhoenixDifficultyTitle("Expert Lv. 7", 26, 13000),
        new PhoenixDifficultyTitle("Expert Lv. 8", 26, 26000),
        new PhoenixDifficultyTitle("Expert Lv. 9", 27, 3500),
        new PhoenixDifficultyTitle("Expert Lv. 10", 27, 7000),
        new PhoenixDifficultyTitle("The Master", 28, 1900),
        new PhoenixCoOpTitle("[CO-OP] Lv.1", 30000),
        new PhoenixCoOpTitle("[CO-OP] Lv.2", 60000),
        new PhoenixCoOpTitle("[CO-OP] Lv.3", 90000),
        new PhoenixCoOpTitle("[CO-OP] Lv.4", 120000),
        new PhoenixCoOpTitle("[CO-OP] Lv.5", 150000),
        new PhoenixCoOpTitle("[CO-OP] Lv.6", 180000),
        new PhoenixCoOpTitle("[CO-OP] Lv.7", 210000),
        new PhoenixCoOpTitle("[CO-OP] Lv.8", 240000),
        new PhoenixCoOpTitle("[CO-OP] Lv.9", 270000),
        new PhoenixCoOpTitle("[CO-OP] Lv.10", 300000),
        new PhoenixCoOpTitle("[CO-OP] ADVANCED", 330000),
        new PhoenixCoOpTitle("[CO-OP] EXPERT", 360000),
        new PhoenixCoOpTitle("[CO-OP] MASTER", 390000),
        new PhoenixBasicTitle("PIU STAFF", "A title given to PIU STAFF", "Officially Assigned"),
        new PhoenixBasicTitle("PIU SUPPORTER", "A title given to PIU supporter (an international PR program)",
            "Officially Assigned"),
        new PhoenixBasicTitle("PIU GUARDIAN",
            "A title given to a user who has greatly helped the PIU (TOs, EOs, community tooling)",
            "Officially Assigned"),
        new PhoenixBasicTitle("PIU ARTIST", "A title given to a PIU Artist",
            "Officially Assigned"),
        new PhoenixBasicTitle("PIU STEPMAKER", "A title given to a PIU Stepmaker"),
        new PhoenixBasicTitle("LOVERS (Gold)", "1000+ CoOp plays", "CoOp"),
        new PhoenixBasicTitle("LOVERS (Silver)", "100+ CoOp plays", "CoOp"),
        new PhoenixBasicTitle("SCROOGE", "Have 10k PP at once"),
        new PhoenixBasicTitle("CHEATER", "SG (0 miss) on level 27 or higher"),
        new PhoenixBasicTitle("Perfect breaker", "Gargoyle Full S21 444,444 or lower score", "Chart Specific"),
        new PhoenixBasicTitle("Human metronome", "Yeo rae a S1", "Chart Specific"),
        new PhoenixBasicTitle("NO SKILLS NO PUMP", "Moonlight D21 SSS+", "Chart Specific"),
        new PhoenixBasicTitle("PUMP IS A SENSE", "Love is a Danger Zone D21 SSS+", "Chart Specific"),
        new PhoenixBasicTitle("B.P.M FOLLOWER", "Beethoven Virus D18 927 Perfects, 1 Great, 2 Misses, 747 Max Combo",
            "Chart Specific"),
        new PhoenixBasicTitle("DOGE MAJOR STOCKHOLDER", "Waltz of Doge D20 Max Combo 888", "Chart Specific"),
        new PhoenixBasicTitle("VVIP MEMBER", "10000 or more plays", "Play Count"),
        new PhoenixBasicTitle("VIP MEMBER", "5000 or more plays", "Play Count"),
        new PhoenixBasicTitle("DIAMOND MEMBER", "1000 or more plays", "Play Count"),
        new PhoenixBasicTitle("Platinum Member", "500 or more plays", "Play Count"),
        new PhoenixBasicTitle("GOLD MEMBER", "100 or more plays", "Play Count"),
        new PhoenixBossBreakerTitle("XX", "1949", ChartType.Double, 28),
        new PhoenixBossBreakerTitle("XX", "ERRORCODE: 0", ChartType.Single, 25),
        new PhoenixBossBreakerTitle("PRIME2", "Shub Sothoth", ChartType.Double, 27),
        new PhoenixBossBreakerTitle("PRIME2", "Shub Sothoth", ChartType.Single, 25),
        new PhoenixBossBreakerTitle("PRIME", "Paradoxx", ChartType.Double, 28),
        new PhoenixBossBreakerTitle("PRIME", "Paradoxx", ChartType.Single, 26),
        new PhoenixBossBreakerTitle("FIESTA2", "Ignis Fatuus", ChartType.Double, 25),
        new PhoenixBossBreakerTitle("FIESTA2", "Ignis Fatuus", ChartType.Single, 22),
        new PhoenixBossBreakerTitle("FIESTA EX", "Vacuum Cleaner", ChartType.Double, 26),
        new PhoenixBossBreakerTitle("FIESTA EX", "Vacuum Cleaner", ChartType.Single, 25),
        new PhoenixBossBreakerTitle("FIESTA", "Vacuum", ChartType.Double, 25),
        new PhoenixBossBreakerTitle("FIESTA", "Vacuum", ChartType.Single, 23),
        new PhoenixBossBreakerTitle("NXA", "Final Audition Ep. 2-X", ChartType.Double, 24),
        new PhoenixBossBreakerTitle("NXA", "Final Audition Ep. 2-X", ChartType.Single, 23),
        new PhoenixBossBreakerTitle("NX2", "Banya-P Guitar Remix", ChartType.Double, 24),
        new PhoenixBossBreakerTitle("NX2", "Banya-P Guitar Remix", ChartType.Single, 22),
        new PhoenixBossBreakerTitle("NX", "BEMERA", ChartType.Double, 26),
        new PhoenixBossBreakerTitle("NX", "BEMERA", ChartType.Single, 24),
        new PhoenixBossBreakerTitle("ZERO", "Love is a Danger Zone pt. 2", ChartType.Double, 24),
        new PhoenixBossBreakerTitle("ZERO", "Love is a Danger Zone pt. 2", ChartType.Single, 22),
        new PhoenixBossBreakerTitle("EXCEED2", "Canon D", ChartType.Double, 23),
        new PhoenixBossBreakerTitle("EXCEED2", "Canon D", ChartType.Single, 20),
        new PhoenixBossBreakerTitle("EXCEED", "Dignity", ChartType.Double, 24),
        new PhoenixBossBreakerTitle("EXCEED", "Dignity", ChartType.Single, 21),
        new PhoenixBossBreakerTitle("THE PREX3", "Bee", ChartType.Single, 17, false),
        new PhoenixBossBreakerTitle("THE REBIRTH", "Love is a Danger Zone", ChartType.Single, 17, false),
        new PhoenixBossBreakerTitle("EXTRA", "Radetzky Can Can", ChartType.Double, 18, false),
        new PhoenixBossBreakerTitle("Perfect Collection", "Slam", ChartType.Single, 18, false),
        new PhoenixBossBreakerTitle("The O.B.G SE", "Mr. Larpus", ChartType.Single, 15, false),
        new PhoenixBossBreakerTitle("The O.B.G", "Turkey March", ChartType.Single, 12, false),
        new PhoenixBossBreakerTitle("The 2nd", "Extravaganza", ChartType.Single, 11, false),
        new PhoenixBossBreakerTitle("The 1st", "Another Truth", ChartType.Single, 6, false),
        new PhoenixBasicTitle("PERFECT GAMER (Platinum)", "3000+ PGs", "Plates"),
        new PhoenixBasicTitle("PERFECT GAMER (Gold)", "1000+ PGs", "Plates"),
        new PhoenixBasicTitle("PERFECT GAMER (Silver)", "500+ PGs", "Plates"),
        new PhoenixBasicTitle("PERFECT GAMER (Bronze)", "100+ PGs", "Plates"),
        new PhoenixBasicTitle("ULTIMATE GAMER (Platinum)", "3000+ UGs", "Plates"),
        new PhoenixBasicTitle("ULTIMATE GAMER (Gold)", "1000+ UGs", "Plates"),
        new PhoenixBasicTitle("ULTIMATE GAMER (Silver)", "500+ UGs", "Plates"),
        new PhoenixBasicTitle("ULTIMATE GAMER (Bronze)", "100+ UGs", "Plates"),
        new PhoenixBasicTitle("EXTREME GAMER (Platinum)", "3000+ EGs", "Plates"),
        new PhoenixBasicTitle("EXTREME GAMER (Gold)", "1000+ EGs", "Plates"),
        new PhoenixBasicTitle("EXTREME GAMER (Silver)", "500+ EGs", "Plates"),
        new PhoenixBasicTitle("EXTREME GAMER (Bronze)", "100+ EGs", "Plates"),
        new PhoenixBasicTitle("SUPERB GAMER (Platinum)", "3000+ SGs", "Plates"),
        new PhoenixBasicTitle("SUPERB GAMER (Gold)", "1000+ SGs", "Plates"),
        new PhoenixBasicTitle("SUPERB GAMER (Silver)", "500+ SGs", "Plates"),
        new PhoenixBasicTitle("SUPERB GAMER (Bronze)", "100+ SGs", "Plates"),
        new PhoenixBasicTitle("MARVELOUS GAMER (Platinum)", "3000+ MGs", "Plates"),
        new PhoenixBasicTitle("MARVELOUS GAMER (Gold)", "1000+ MGs", "Plates"),
        new PhoenixBasicTitle("MARVELOUS GAMER (Silver)", "500+ MGs", "Plates"),
        new PhoenixBasicTitle("MARVELOUS GAMER (Bronze)", "100+ MGs", "Plates"),
        new PhoenixBasicTitle("TALENTED GAMER (Platinum)", "3000+ TGs", "Plates"),
        new PhoenixBasicTitle("TALENTED GAMER (Gold)", "1000+ TGs", "Plates"),
        new PhoenixBasicTitle("TALENTED GAMER (Silver)", "500+ TGs", "Plates"),
        new PhoenixBasicTitle("TALENTED GAMER (Bronze)", "100+ TGs", "Plates"),
        new PhoenixBasicTitle("FAIR GAMER (Platinum)", "3000+ FGs", "Plates"),
        new PhoenixBasicTitle("FAIR GAMER (Gold)", "1000+ FGs", "Plates"),
        new PhoenixBasicTitle("FAIR GAMER (Silver)", "500+ FGs", "Plates"),
        new PhoenixBasicTitle("FAIR GAMER (Bronze)", "100+ FGs", "Plates"),
        new PhoenixBasicTitle("ROUGH GAMER (Platinum)", "3000+ RGs", "Plates"),
        new PhoenixBasicTitle("ROUGH GAMER (Gold)", "1000+ RGs", "Plates"),
        new PhoenixBasicTitle("ROUGH GAMER (Silver)", "500+ RGs", "Plates"),
        new PhoenixBasicTitle("ROUGH GAMER (Bronze)", "100+ RGs", "Plates"),
        new PhoenixBasicTitle("SPECIALIST", "Earn all skill titles", "Skill"),
        new PhoenixBasicTitle("[BRACKET] EXPERT", "Earn Bracket Titles 1-10", "Skill"),
        new PhoenixSkillTitle("BRACKET", 10, "Phalanx \"RS2018 edit\"", ChartType.Double, 24),
        new PhoenixSkillTitle("BRACKET", 9, "Scorpion King", ChartType.Double, 23),
        new PhoenixSkillTitle("BRACKET", 8, "Pop Sequence", ChartType.Double, 23),
        new PhoenixSkillTitle("BRACKET", 7, "What Happened", ChartType.Double, 23),
        new PhoenixSkillTitle("BRACKET", 6, "Meteo5cience (GADGET mix)", ChartType.Double, 22),
        new PhoenixSkillTitle("BRACKET", 5, "Phalanx \"RS2018 edit\"", ChartType.Single, 22),
        new PhoenixSkillTitle("BRACKET", 4, "What Happened", ChartType.Single, 21),
        new PhoenixSkillTitle("BRACKET", 3, "Meteo5cience (GADGET mix)", ChartType.Single, 21),
        new PhoenixSkillTitle("BRACKET", 2, "Mad5cience", ChartType.Single, 20),
        new PhoenixSkillTitle("BRACKET", 1, "Allegro furioso", ChartType.Double, 20),
        new PhoenixBasicTitle("[HALF] EXPERT", "Earn Half Titles 1-10", "Skill"),
        new PhoenixSkillTitle("HALF", 10, "Imprinting", ChartType.Double, 24),
        new PhoenixSkillTitle("HALF", 9, "Love is a Danger Zone 2 Try to B.P.M", ChartType.Double, 23),
        new PhoenixSkillTitle("HALF", 8, "Redline", ChartType.Double, 22),
        new PhoenixSkillTitle("HALF", 7, "Witch Doctor #1", ChartType.Double, 21),
        new PhoenixSkillTitle("HALF", 6, "Utsushiyo No Kaze feat. Kana", ChartType.Double, 20),
        new PhoenixSkillTitle("HALF", 5, "Phantom", ChartType.Double, 19),
        new PhoenixSkillTitle("HALF", 4, "Super Fantasy", ChartType.Double, 18),
        new PhoenixSkillTitle("HALF", 3, "Shub Niggurath", ChartType.Double, 18),
        new PhoenixSkillTitle("HALF", 2, "Butterfly", ChartType.Double, 17),
        new PhoenixSkillTitle("HALF", 1, "Mopemope", ChartType.Double, 17),
        new PhoenixBasicTitle("[GIMMICK] EXPERT", "Earn Gimmick Titles 1-10", "Skill"),
        new PhoenixSkillTitle("GIMMICK", 10, "Everybody Got 2 Know", ChartType.Single, 21),
        new PhoenixSkillTitle("GIMMICK", 9, "8 6", ChartType.Single, 20),
        new PhoenixSkillTitle("GIMMICK", 8, "Twist of Fate (feat. Ruriling)", ChartType.Single, 19),
        new PhoenixSkillTitle("GIMMICK", 7, "Nakakapagpabagabag", ChartType.Single, 19),
        new PhoenixSkillTitle("GIMMICK", 6, "Miss S' story", ChartType.Single, 19),
        new PhoenixSkillTitle("GIMMICK", 5, "Rock the house - SHORT CUT -", ChartType.Single, 18),
        new PhoenixSkillTitle("GIMMICK", 4, "Come to Me", ChartType.Single, 17),
        new PhoenixSkillTitle("GIMMICK", 3, "Ugly Dee", ChartType.Single, 17),
        new PhoenixSkillTitle("GIMMICK", 2, "8 6", ChartType.Single, 16),
        new PhoenixSkillTitle("GIMMICK", 1, "Yeo rae a", ChartType.Single, 13),
        new PhoenixBasicTitle("[DRILL] EXPERT", "Earn Drill Titles 1-10", "Skill"),
        new PhoenixSkillTitle("DRILL", 10, "WI-EX-DOC-VA", ChartType.Double, 24),
        new PhoenixSkillTitle("DRILL", 9, "Witch Doctor", ChartType.Double, 23),
        new PhoenixSkillTitle("DRILL", 8, "Rock the house", ChartType.Double, 22),
        new PhoenixSkillTitle("DRILL", 7, "Sorceress Elise", ChartType.Single, 21),
        new PhoenixSkillTitle("DRILL", 6, "Overblow", ChartType.Single, 20),
        new PhoenixSkillTitle("DRILL", 5, "Vacuum", ChartType.Single, 19),
        new PhoenixSkillTitle("DRILL", 4, "Moonlight", ChartType.Single, 18),
        new PhoenixSkillTitle("DRILL", 3, "Gun Rock", ChartType.Single, 17),
        new PhoenixSkillTitle("DRILL", 2, "Vook", ChartType.Single, 16),
        new PhoenixSkillTitle("DRILL", 1, "Hellfire", ChartType.Single, 15),
        new PhoenixBasicTitle("[RUN] EXPERT", "Earn Run Titles 1-10", "Skill"),
        new PhoenixSkillTitle("RUN", 10, "Yog-Sothoth", ChartType.Double, 24),
        new PhoenixSkillTitle("RUN", 9, "Baroque Virus - FULL SONG -", ChartType.Double, 23),
        new PhoenixSkillTitle("RUN", 8, "Gargoyle - FULL SONG -", ChartType.Double, 22),
        new PhoenixSkillTitle("RUN", 7, "Sarabande", ChartType.Double, 21),
        new PhoenixSkillTitle("RUN", 6, "Bee", ChartType.Double, 20),
        new PhoenixSkillTitle("RUN", 5, "Napalm", ChartType.Single, 19),
        new PhoenixSkillTitle("RUN", 4, "Gothique Resonance", ChartType.Single, 18),
        new PhoenixSkillTitle("RUN", 3, "Pavane", ChartType.Single, 17),
        new PhoenixSkillTitle("RUN", 2, "Super Fantasy", ChartType.Single, 16),
        new PhoenixSkillTitle("RUN", 1, "Switronic", ChartType.Single, 15),
        new PhoenixBasicTitle("[TWIST] EXPERT", "Earn Twist Titles 1-10", "Skill"),
        new PhoenixSkillTitle("TWIST", 10, "Bee", ChartType.Double, 24),
        new PhoenixSkillTitle("TWIST", 9, "Love Is A Danger Zone(Cranky Mix)", ChartType.Double, 23),
        new PhoenixSkillTitle("TWIST", 8, "Super Fantasy", ChartType.Double, 22),
        new PhoenixSkillTitle("TWIST", 7, "Love is a Danger Zone", ChartType.Double, 21),
        new PhoenixSkillTitle("TWIST", 6, "Witch Doctor #1", ChartType.Double, 20),
        new PhoenixSkillTitle("TWIST", 5, "U GOT 2 KNOW", ChartType.Single, 19),
        new PhoenixSkillTitle("TWIST", 4, "Solitary 2", ChartType.Single, 18),
        new PhoenixSkillTitle("TWIST", 3, "U Got Me Rocking", ChartType.Single, 17),
        new PhoenixSkillTitle("TWIST", 2, "Street show down", ChartType.Single, 16),
        new PhoenixSkillTitle("TWIST", 1, "Scorpion King", ChartType.Single, 15),
        new PhoenixBasicTitle("A huge fan of SPHAM", "500+ plays on SPHAM charts", "Step Artist"),
        new PhoenixBasicTitle("SPHAM FOLLOWER", "100+ plays on SPHAM charts", "Step Artist"),
        new PhoenixBasicTitle("A huge fan of CONRAD", "500+ plays on CONRAD charts", "Step Artist"),
        new PhoenixBasicTitle("CONRAD FOLLOWER", "100+ playso n CONRAD charts", "Step Artist"),
        new PhoenixBasicTitle("A huge fan of DULKI", "500+ plays on Dulki charts", "Step Artist"),
        new PhoenixBasicTitle("DULKI FOLLOWER", "100+ plays on Dulki charts", "Step Artist"),
        new PhoenixBasicTitle("A huge fan of SUNNY", "500+ plays on Sunny charts", "Step Artist"),
        new PhoenixBasicTitle("SUNNY FOLLOWER", "100+ plays on Sunny charts", "Step Artist"),
        new PhoenixBasicTitle("A huge fan of FEFEMZ", "500+ plays on FEFEMZ charts", "Step Artist"),
        new PhoenixBasicTitle("FEFEMZ FOLLOWER", "100+ plays on FEFEMZ charts", "Step Artist"),
        new PhoenixBasicTitle("A huge fan of EXC", "500+ plays on EXC charts", "Step Artist"),
        new PhoenixBasicTitle("EXC FOLLOWER", "100+ plays on EXC charts", "Step Artist")
    };

    private static readonly IDictionary<Name, PhoenixTitle> TitleLookup = Titles.ToDictionary(n => n.Name);

    public static PhoenixTitle GetTitleByName(Name name)
    {
        return TitleLookup[name];
    }

    public static IEnumerable<PhoenixTitle> BuildList()
    {
        return Titles.ToArray();
    }

    public static IEnumerable<PhoenixTitleProgress> BuildProgress(IDictionary<Guid, Chart> charts,
        IEnumerable<RecordedPhoenixScore> attempts,
        ISet<Name> completedTitles)
    {
        var progress = Titles.Select(t => new PhoenixTitleProgress(t)).ToImmutableArray();
        foreach (var attempt in attempts)
        foreach (var title in progress)
        {
            title.ApplyAttempt(charts[attempt.ChartId], attempt);
            if (completedTitles.Contains(title.Title.Name))
                title.Complete();
        }

        return progress;
    }
}