using System.Collections.Immutable;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     Phoenix 2's title list â€” all 272 titles scraped from the live my_page/title.php
///     (2026-07-09, authenticated crawl; requirement text preserved verbatim, typos
///     included). Names are the site's exact data-name values so site-detection matches.
///     Chart-specific titles reference songs by the requirement's naming; ones whose song
///     isn't in the Phoenix 2 catalog yet never progress until the song imports.
///     The [S]/[D] ladders gate on the per-type top-50 pools and the Total ladder on the
///     merged top-50 across both types, all computed by <see cref="BuildProgress" /> with
///     the official Phoenix 2 formula.
/// </summary>
public static class Phoenix2TitleList
{
    private static readonly PhoenixTitle[] Titles =
    {
        // ---- Default ----
        new PhoenixBasicTitle("BEGINNER", "Default title"),

        // ---- Step-artist play counts (site-detected only) ----
        new PhoenixBasicTitle("EXC FOLLOWER", "[EXC STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("EXC ENTHUSIAST", "[EXC STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("EXC DEVOTEE", "[EXC STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("DULKI FOLLOWER", "[DULKI STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("DULKI ENTHUSIAST", "[DULKI STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("DULKI DEVOTEE", "[DULKI STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("FEFEMZ FOLLOWER", "[FEFEMZ STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("FEFEMZ ENTHUSIAST", "[FEFEMZ STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("FEFEMZ DEVOTEE", "[FEFEMZ STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("CONRAD FOLLOWER", "[CONRAD STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("CONRAD ENTHUSIAST", "[CONRAD STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("CONRAD DEVOTEE", "[CONRAD STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("NIMGO FOLLOWER", "[NIMGO STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("NIMGO ENTHUSIAST", "[NIMGO STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("NIMGO DEVOTEE", "[NIMGO STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("SUNNY FOLLOWER", "[SUNNY STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("SUNNY ENTHUSIAST", "[SUNNY STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("SUNNY DEVOTEE", "[SUNNY STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("SPHAM FOLLOWER", "[SPHAM STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("SPHAM ENTHUSIAST", "[SPHAM STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("SPHAM DEVOTEE", "[SPHAM STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("JUNARE FOLLOWER", "[JUNARE STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("JUNARE ENTHUSIAST", "[JUNARE STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("JUNARE DEVOTEE", "[JUNARE STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("GGWANG FOLLOWER", "[GGWANG STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("GGWANG ENTHUSIAST", "[GGWANG STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("GGWANG DEVOTEE", "[GGWANG STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("DIESEL FOLLOWER", "[DIESEL STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("DIESEL ENTHUSIAST", "[DIESEL STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("DIESEL DEVOTEE", "[DIESEL STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("REFOS FOLLOWER", "[REFOS STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("REFOS ENTHUSIAST", "[REFOS STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("REFOS DEVOTEE", "[REFOS STEP] 1000+ Plays", "Step Artist"),
        new PhoenixBasicTitle("ABYSS FOLLOWER", "[ABYSS STEP] 100+ Plays", "Step Artist"),
        new PhoenixBasicTitle("ABYSS ENTHUSIAST", "[ABYSS STEP] 500+ Plays", "Step Artist"),
        new PhoenixBasicTitle("ABYSS DEVOTEE", "[ABYSS STEP] 1000+ Plays", "Step Artist"),

        // ---- Play counts (site-detected only) ----
        new PhoenixBasicTitle("GOLD MEMBER", "[Play count] 100 or more", "Play Count"),
        new PhoenixBasicTitle("PLATINUM MEMBER", "[Play count] 500 or more", "Play Count"),
        new PhoenixBasicTitle("DIAMOND MEMBER", "[Play count] 1000 or more", "Play Count"),
        new PhoenixBasicTitle("VIP MEMBER", "[Play count] 3000 or more", "Play Count"),
        new PhoenixBasicTitle("VVIP MEMBER", "[Play count] 5000 or more", "Play Count"),
        new PhoenixBasicTitle("THE BLACK", "[Play count] 10000 or more", "Play Count"),

        // ---- Chart-specific and misc badges (site-detected only) ----
        new PhoenixBasicTitle("DOGE MAJOR STOCKHOLDER", "[Waltz of Doge D20] Max Combo of 888", "Chart Specific"),
        new PhoenixBasicTitle("B.P.M FOLLOWER", "[Beethoven Virus D18] Perfect 927 / Great 1 / Miss 2 / Max Combo 747", "Chart Specific"),

        // ---- Chart-specific grade badges ----
        new Phoenix2ChartGradeTitle("PUMP IS A SENSE", "Chart Specific", "Love is a Danger Zone", ChartType.Double, 21, PhoenixLetterGrade.SSSPlus),
        new Phoenix2ChartGradeTitle("NO SKILLS NO PUMP", "Chart Specific", "Moonlight", ChartType.Double, 22, PhoenixLetterGrade.SSSPlus),

        // ---- Chart-specific and misc badges (site-detected only) ----
        new PhoenixBasicTitle("HUMAN METRONOME", "[Yeo rae a S1] 180,000 Point or less", "Chart Specific"),
        new PhoenixBasicTitle("PERFECT BREAKER", "[Gargoyle - FULL SONG - S21] 444,444 Point or less", "Chart Specific"),
        new PhoenixBasicTitle("CHEATER", "[Lv.27 over] SUPERB GAME or more", "Chart Specific"),

        // ---- CO-OP play counts (site-detected only) ----
        // The site names the first two "LOVERS" (duplicate data-name) and masks the third;
        // suffixed like the Phoenix list. Site-detection never matches these names.
        new PhoenixBasicTitle("LOVERS (Silver)", "[CO-OP Step] 100+ Plays", "CO-OP"),
        new PhoenixBasicTitle("LOVERS (Gold)", "[CO-OP Step] 1000+ Plays", "CO-OP"),
        new PhoenixBasicTitle("LOVERS (Platinum)", "[CO-OP Step] 5000+ Plays", "CO-OP"),

        // ---- Chart-specific and misc badges (site-detected only) ----
        new PhoenixBasicTitle("GOD OF CONTROL", "[Gargoyle - FULL SONG - S21] Clear with 160 or more misses", "Chart Specific"),
        new PhoenixBasicTitle("GRAND DADDY", "[Big Daddy S21] Clear with AV999", "Chart Specific"),

        // ---- Singles PUMBILITY ladder ----
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.1", PumbilityPool.Singles, 5000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.2", PumbilityPool.Singles, 6000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.3", PumbilityPool.Singles, 7000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.4", PumbilityPool.Singles, 8000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.5", PumbilityPool.Singles, 9000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.6", PumbilityPool.Singles, 10000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.7", PumbilityPool.Singles, 11000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.8", PumbilityPool.Singles, 12000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.9", PumbilityPool.Singles, 13000),
        new Phoenix2PumbilityTitle("[S] INTERMEDIATE LV.10", PumbilityPool.Singles, 14000),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.1", PumbilityPool.Singles, 15000),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.2", PumbilityPool.Singles, 15250),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.3", PumbilityPool.Singles, 15500),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.4", PumbilityPool.Singles, 15750),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.5", PumbilityPool.Singles, 16000),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.6", PumbilityPool.Singles, 16250),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.7", PumbilityPool.Singles, 16500),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.8", PumbilityPool.Singles, 16750),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.9", PumbilityPool.Singles, 17000),
        new Phoenix2PumbilityTitle("[S] ADVANCED LV.10", PumbilityPool.Singles, 17250),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.1", PumbilityPool.Singles, 17500),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.2", PumbilityPool.Singles, 17700),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.3", PumbilityPool.Singles, 17900),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.4", PumbilityPool.Singles, 18100),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.5", PumbilityPool.Singles, 18300),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.6", PumbilityPool.Singles, 18500),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.7", PumbilityPool.Singles, 18600),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.8", PumbilityPool.Singles, 18700),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.9", PumbilityPool.Singles, 18800),
        new Phoenix2PumbilityTitle("[S] EXPERT LV.10", PumbilityPool.Singles, 18900),
        new Phoenix2PumbilityTitle("SINGLE MASTER", PumbilityPool.Singles, 19000),

        // ---- Doubles PUMBILITY ladder ----
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.1", PumbilityPool.Doubles, 5000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.2", PumbilityPool.Doubles, 6000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.3", PumbilityPool.Doubles, 7000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.4", PumbilityPool.Doubles, 8000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.5", PumbilityPool.Doubles, 9000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.6", PumbilityPool.Doubles, 10000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.7", PumbilityPool.Doubles, 11000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.8", PumbilityPool.Doubles, 12000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.9", PumbilityPool.Doubles, 13000),
        new Phoenix2PumbilityTitle("[D] INTERMEDIATE LV.10", PumbilityPool.Doubles, 14000),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.1", PumbilityPool.Doubles, 15000),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.2", PumbilityPool.Doubles, 15300),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.3", PumbilityPool.Doubles, 15600),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.4", PumbilityPool.Doubles, 15900),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.5", PumbilityPool.Doubles, 16200),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.6", PumbilityPool.Doubles, 16500),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.7", PumbilityPool.Doubles, 16800),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.8", PumbilityPool.Doubles, 17100),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.9", PumbilityPool.Doubles, 17400),
        new Phoenix2PumbilityTitle("[D] ADVANCED LV.10", PumbilityPool.Doubles, 17700),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.1", PumbilityPool.Doubles, 18000),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.2", PumbilityPool.Doubles, 18200),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.3", PumbilityPool.Doubles, 18400),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.4", PumbilityPool.Doubles, 18600),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.5", PumbilityPool.Doubles, 18800),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.6", PumbilityPool.Doubles, 19000),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.7", PumbilityPool.Doubles, 19100),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.8", PumbilityPool.Doubles, 19200),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.9", PumbilityPool.Doubles, 19300),
        new Phoenix2PumbilityTitle("[D] EXPERT LV.10", PumbilityPool.Doubles, 19400),
        new Phoenix2PumbilityTitle("DOUBLE MASTER", PumbilityPool.Doubles, 19500),

        // ---- Total PUMBILITY tiers ----
        // Names are masked ("????") on title.php until earned. [P.B] BRONZE was observed
        // worn on the live rankings; the rest are placeholders.
        // TODO(P2-titles): replace placeholder names as players reveal them post-launch.
        new Phoenix2PumbilityTitle("[P.B] BRONZE", PumbilityPool.Total, 10000),
        new Phoenix2PumbilityTitle("[P.B] ??? 12500", PumbilityPool.Total, 12500),
        new Phoenix2PumbilityTitle("[P.B] ??? 15000", PumbilityPool.Total, 15000),
        new Phoenix2PumbilityTitle("[P.B] ??? 16000", PumbilityPool.Total, 16000),
        new Phoenix2PumbilityTitle("[P.B] ??? 17000", PumbilityPool.Total, 17000),
        new Phoenix2PumbilityTitle("[P.B] ??? 18000", PumbilityPool.Total, 18000),
        new Phoenix2PumbilityTitle("[P.B] ??? 19000", PumbilityPool.Total, 19000),
        new Phoenix2PumbilityTitle("[P.B] ??? 20000", PumbilityPool.Total, 20000),

        // ---- Skill ladders (chart + grade) ----
        new Phoenix2ChartGradeTitle("[TWIST S] LV.1", "TWIST S", "Scorpion King", ChartType.Single, 15, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.2", "TWIST S", "Street show down", ChartType.Single, 16, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.3", "TWIST S", "U Got Me Rocking", ChartType.Single, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.4", "TWIST S", "Solitary 2", ChartType.Single, 18, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.5", "TWIST S", "U Got 2 Know", ChartType.Single, 19, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.6", "TWIST S", "Canon D", ChartType.Single, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.7", "TWIST S", "Love Is A Danger Zone (Cranky Mix)", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.8", "TWIST S", "DUEL", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.9", "TWIST S", "Love is a Danger Zone pt.2", ChartType.Single, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST S] LV.10", "TWIST S", "Uranium", ChartType.Single, 22, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[TWIST S] EXPERT", "[TWIST S] Earn 10 Titles", "TWIST S", new Name[] { "TWIST S" }, 10),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.1", "TWIST D", "Redline", ChartType.Double, 16, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.2", "TWIST D", "Mr. Larpus", ChartType.Double, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.3", "TWIST D", "Dignity", ChartType.Double, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.4", "TWIST D", "Final Audition 2", ChartType.Double, 18, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.5", "TWIST D", "Final Audition", ChartType.Double, 19, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.6", "TWIST D", "Witch Doctor #1", ChartType.Double, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.7", "TWIST D", "Love is a Danger Zone", ChartType.Double, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.8", "TWIST D", "Super Fantasy", ChartType.Double, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.9", "TWIST D", "Love Is A Danger Zone (Cranky Mix)", ChartType.Double, 23, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[TWIST D] LV.10", "TWIST D", "Love is a Danger Zone pt.2", ChartType.Double, 24, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[TWIST D] EXPERT", "[TWIST D] Earn 10 Titles", "TWIST D", new Name[] { "TWIST D" }, 10),
        new Phoenix2ChartGradeTitle("[RUN S] LV.1", "RUN S", "Switronic", ChartType.Single, 15, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.2", "RUN S", "Super Fantasy", ChartType.Single, 16, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.3", "RUN S", "Pavane", ChartType.Single, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.4", "RUN S", "Gothique Resonance", ChartType.Single, 18, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.5", "RUN S", "Bee", ChartType.Single, 19, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.6", "RUN S", "Jupin - SHORT CUT -", ChartType.Single, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.7", "RUN S", "Horang Pungryuga", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.8", "RUN S", "Conflict", ChartType.Single, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.9", "RUN S", "Gargoyle - FULL SONG -", ChartType.Single, 23, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN S] LV.10", "RUN S", "Final Audition Ep. 2-X", ChartType.Single, 24, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[RUN S] EXPERT", "[RUN S] Earn 10 Titles", "RUN S", new Name[] { "RUN S" }, 10),
        new Phoenix2ChartGradeTitle("[RUN D] LV.1", "RUN D", "Final Audition", ChartType.Double, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.2", "RUN D", "Yog-Sothoth", ChartType.Double, 18, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.3", "RUN D", "HTTP", ChartType.Double, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.4", "RUN D", "Bee", ChartType.Double, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.5", "RUN D", "Conflict", ChartType.Double, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.6", "RUN D", "Sarabande", ChartType.Double, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.7", "RUN D", "Destination - SHORT CUT -", ChartType.Double, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.8", "RUN D", "Gargoyle - FULL SONG -", ChartType.Double, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.9", "RUN D", "Baroque Virus - FULL SONG -", ChartType.Double, 23, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[RUN D] LV.10", "RUN D", "Canon D - FULL SONG -", ChartType.Double, 24, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[RUN D] EXPERT", "[RUN D] Earn 10 Titles", "RUN D", new Name[] { "RUN D" }, 10),
        new Phoenix2ChartGradeTitle("[DRILL] LV.1", "DRILL", "Hellfire", ChartType.Single, 15, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.2", "DRILL", "Vook", ChartType.Single, 16, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.3", "DRILL", "Gun Rock", ChartType.Single, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.4", "DRILL", "Moonlight", ChartType.Single, 18, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.5", "DRILL", "Vacuum", ChartType.Single, 19, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.6", "DRILL", "Guitar Man", ChartType.Single, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.7", "DRILL", "WI-EX-DOC-VA", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.8", "DRILL", "Overblow", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.9", "DRILL", "Sorceress Elise", ChartType.Single, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[DRILL] LV.10", "DRILL", "Death Moon - SHORT CUT -", ChartType.Single, 23, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[DRILL] EXPERT", "[DRILL] Earn 10 Titles", "DRILL", new Name[] { "DRILL" }, 10),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.1", "GIMMICK", "STAGER", ChartType.Single, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.2", "GIMMICK", "Ugly Dee", ChartType.Single, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.3", "GIMMICK", "Rock the house - SHORT CUT -", ChartType.Single, 18, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.4", "GIMMICK", "Come to Me", ChartType.Single, 18, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.5", "GIMMICK", "Miss S' story", ChartType.Single, 19, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.6", "GIMMICK", "Nakakapagpabagabag", ChartType.Single, 19, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.7", "GIMMICK", "8 6", ChartType.Single, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.8", "GIMMICK", "Tales of Pumpnia", ChartType.Single, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.9", "GIMMICK", "CHAOS AGAIN", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[GIMMICK] LV.10", "GIMMICK", "Everybody Got 2 Know", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[GIMMICK] EXPERT", "[GIMMICK] Earn 10 Titles", "GIMMICK", new Name[] { "GIMMICK" }, 10),
        new Phoenix2ChartGradeTitle("[SLOW] LV.1", "SLOW", "Yeo rae a", ChartType.Single, 13, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.2", "SLOW", "Twist of Fate", ChartType.Single, 19, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.3", "SLOW", "Moonlight", ChartType.Single, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.4", "SLOW", "Night Duty", ChartType.Single, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.5", "SLOW", "Karyawisata", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.6", "SLOW", "Take Out", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.7", "SLOW", "Twist of Fate", ChartType.Double, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.8", "SLOW", "Moonlight", ChartType.Double, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.9", "SLOW", "Take Out", ChartType.Double, 23, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[SLOW] LV.10", "SLOW", "Banya-P Guitar Remix", ChartType.Single, 23, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[SLOW] EXPERT", "[SLOW] Earn 10 Titles", "SLOW", new Name[] { "SLOW" }, 10),
        new Phoenix2ChartGradeTitle("[HALF] LV.1", "HALF", "Mopemope", ChartType.Double, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.2", "HALF", "Butterfly", ChartType.Double, 17, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.3", "HALF", "Shub Niggurath", ChartType.Double, 18, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.4", "HALF", "Super Fantasy", ChartType.Double, 19, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.5", "HALF", "Phantom -Intermezzo-", ChartType.Double, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.6", "HALF", "Phantom", ChartType.Double, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.7", "HALF", "Utsushiyo No Kaze", ChartType.Double, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.8", "HALF", "Redline", ChartType.Double, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.9", "HALF", "Love is a Danger Zone 2 Try To B.P.M", ChartType.Double, 23, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[HALF] LV.10", "HALF", "Imprinting", ChartType.Double, 24, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[HALF] EXPERT", "[HALF] Earn 10 Titles", "HALF", new Name[] { "HALF" }, 10),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.1", "BRACKET", "Allegro Furioso", ChartType.Double, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.2", "BRACKET", "Mad5cience", ChartType.Single, 20, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.3", "BRACKET", "Pop Sequence", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.4", "BRACKET", "Meteo5cience", ChartType.Single, 21, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.5", "BRACKET", "Phalanx", ChartType.Single, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.6", "BRACKET", "Meteo5cience", ChartType.Double, 22, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.7", "BRACKET", "What Happened", ChartType.Double, 23, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.8", "BRACKET", "Scorpion King", ChartType.Double, 23, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.9", "BRACKET", "Phalanx", ChartType.Double, 24, PhoenixLetterGrade.SSS),
        new Phoenix2ChartGradeTitle("[BRACKET] LV.10", "BRACKET", "Hymn of Golden Glory - SHORT CUT -", ChartType.Double, 24, PhoenixLetterGrade.SSS),
        new Phoenix2TitleSetTitle("[BRACKET] EXPERT", "[BRACKET] Earn 10 Titles", "BRACKET", new Name[] { "BRACKET" }, 10),

        // ---- Skill meta ----
        new Phoenix2TitleSetTitle("SPECIALIST", "Earn all skill titles", "Misc.",
            new Name[] { "TWIST S", "TWIST D", "RUN S", "RUN D", "DRILL", "GIMMICK", "SLOW", "HALF", "BRACKET" }, 90),

        // ---- CO-OP rating ladder (site-detected only) ----
        // TODO(P2-titles): the in-game "CO-OP Rating" formula is unknown â€” these stay
        // site-detected until it can be reverse-engineered from live CoOp data.
        new PhoenixBasicTitle("[CO-OP] LV.1", "[CO-OP Rating] 1000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.2", "[CO-OP Rating] 2000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.3", "[CO-OP Rating] 3000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.4", "[CO-OP Rating] 4000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.5", "[CO-OP Rating] 5000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.6", "[CO-OP Rating] 6000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.7", "[CO-OP Rating] 7000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.8", "[CO-OP Rating] 8000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.9", "[CO-OP Rating] 9000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] LV.10", "[CO-OP Rating] 10000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] ADVANCED", "[CO-OP Rating] 12000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] EXPERT", "[CO-OP Rating] 14000+", "CO-OP"),
        new PhoenixBasicTitle("[CO-OP] MASTER", "[CO-OP Rating] 16000+", "CO-OP"),

        // ---- Boss breakers (chart clears) ----
        new Phoenix2ChartClearTitle("[THE 1ST] BOSS BREAKER", "Another Truth", ChartType.Single, 6),
        new Phoenix2ChartClearTitle("[THE 2ND] BOSS BREAKER", "Extravaganza", ChartType.Single, 11),
        new Phoenix2ChartClearTitle("[THE O.B.G] BOSS BREAKER", "Turkey March", ChartType.Single, 12),
        new Phoenix2ChartClearTitle("[THE O.B.G SE] BOSS BREAKER", "Mr. Larpus", ChartType.Single, 15),
        new Phoenix2ChartClearTitle("[PERFECT COLLECTION] BOSS BREAKER", "Slam", ChartType.Single, 18),
        new Phoenix2ChartClearTitle("[EXTRA] BOSS BREAKER", "Radetzky Can Can", ChartType.Double, 18),
        new Phoenix2ChartClearTitle("[THE REBIRTH] BOSS BREAKER", "Love is a Danger Zone", ChartType.Single, 17),
        new Phoenix2ChartClearTitle("[THE PREX3] BOSS BREAKER", "Bee", ChartType.Single, 17),
        new Phoenix2ChartClearTitle("[EXCEED] SINGLE BOSS BREAKER", "Dignity", ChartType.Single, 21),
        new Phoenix2ChartClearTitle("[EXCEED] DOUBLE BOSS BREAKER", "Dignity", ChartType.Double, 25),
        new Phoenix2ChartClearTitle("[EXCEED2] SINGLE BOSS BREAKER", "Canon D", ChartType.Single, 20),
        new Phoenix2ChartClearTitle("[EXCEED2] DOUBLE BOSS BREAKER", "Canon D", ChartType.Double, 23),
        new Phoenix2ChartClearTitle("[ZERO] SINGLE BOSS BREAKER", "Love is a Danger Zone pt.2", ChartType.Single, 22),
        new Phoenix2ChartClearTitle("[ZERO] DOUBLE BOSS BREAKER", "Love is a Danger Zone pt.2", ChartType.Double, 24),
        new Phoenix2ChartClearTitle("[NX] SINGLE BOSS BREAKER", "BEMERA", ChartType.Single, 24),
        new Phoenix2ChartClearTitle("[NX] DOUBLE BOSS BREAKER", "BEMERA", ChartType.Double, 26),
        new Phoenix2ChartClearTitle("[NX2] SINGLE BOSS BREAKER", "Banya-P Guitar Remix", ChartType.Single, 23),
        new Phoenix2ChartClearTitle("[NX2] DOUBLE BOSS BREAKER", "Banya-P Guitar Remix", ChartType.Double, 25),
        new Phoenix2ChartClearTitle("[NXA] SINGLE BOSS BREAKER", "Final Audition Ep. 2-X", ChartType.Single, 24),
        new Phoenix2ChartClearTitle("[NXA] DOUBLE BOSS BREAKER", "Final Audition Ep. 2-X", ChartType.Double, 24),
        new Phoenix2ChartClearTitle("[FIESTA] SINGLE BOSS BREAKER", "Vacuum", ChartType.Single, 23),
        new Phoenix2ChartClearTitle("[FIESTA] DOUBLE BOSS BREAKER", "Vacuum", ChartType.Double, 25),
        new Phoenix2ChartClearTitle("[FIESTA EX] SINGLE BOSS BREAKER", "Vacuum Cleaner", ChartType.Single, 25),
        new Phoenix2ChartClearTitle("[FIESTA EX] DOUBLE BOSS BREAKER", "Vacuum Cleaner", ChartType.Double, 26),
        new Phoenix2ChartClearTitle("[FIESTA2] SINGLE BOSS BREAKER", "Ignis Fatuus", ChartType.Single, 22),
        new Phoenix2ChartClearTitle("[FIESTA2] DOUBLE BOSS BREAKER", "Ignis Fatuus", ChartType.Double, 25),
        new Phoenix2ChartClearTitle("[PRIME] SINGLE BOSS BREAKER", "Paradoxx", ChartType.Single, 26),
        new Phoenix2ChartClearTitle("[PRIME] DOUBLE BOSS BREAKER", "Paradoxx", ChartType.Double, 28),
        new Phoenix2ChartClearTitle("[PRIME2] SINGLE BOSS BREAKER", "Shub Sothoth", ChartType.Single, 25),
        new Phoenix2ChartClearTitle("[PRIME2] DOUBLE BOSS BREAKER", "Shub Sothoth", ChartType.Double, 27),
        new Phoenix2ChartClearTitle("[XX] SINGLE BOSS BREAKER", "ERRORCODE: 0", ChartType.Single, 25),
        new Phoenix2ChartClearTitle("[XX] DOUBLE BOSS BREAKER", "1949", ChartType.Double, 28),
        new Phoenix2ChartClearTitle("[PHOENIX] SINGLE BOSS BREAKER", "1948", ChartType.Single, 26),
        new Phoenix2ChartClearTitle("[PHOENIX] DOUBLE BOSS BREAKER", "1948", ChartType.Double, null),
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
        var attemptList = attempts as IReadOnlyList<RecordedPhoenixScore> ?? attempts.ToArray();
        var progress = Titles.Select(t => new PhoenixTitleProgress(t)).ToImmutableArray();

        foreach (var attempt in attemptList)
        foreach (var title in progress)
            title.ApplyAttempt(charts[attempt.ChartId], attempt);

        // Each contribution is Base x (grade + plate). [S] and [D] gate on the top 50 of
        // their own type; Total gates on the top 50 across both types.
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var contributions = attemptList
            .Where(a => a.Score != null && charts.ContainsKey(a.ChartId))
            .Select(a => (charts[a.ChartId].Type, Value: scoring.GetScore(charts[a.ChartId].Type,
                charts[a.ChartId].Level, a.Score!.Value, a.Plate ?? PhoenixPlate.RoughGame, a.IsBroken)))
            .ToArray();
        var singles = (int)contributions.Where(c => c.Type == ChartType.Single)
            .Select(c => c.Value).OrderByDescending(v => v).Take(50).Sum();
        var doubles = (int)contributions.Where(c => c.Type == ChartType.Double)
            .Select(c => c.Value).OrderByDescending(v => v).Take(50).Sum();
        var total = (int)contributions
            .Select(c => c.Value).OrderByDescending(v => v).Take(50).Sum();
        foreach (var title in progress)
            if (title.PhoenixTitle is Phoenix2PumbilityTitle pumbility)
                title.ApplyDirectProgress(pumbility.Pool switch
                {
                    PumbilityPool.Singles => singles,
                    PumbilityPool.Doubles => doubles,
                    _ => total
                });

        foreach (var title in progress)
            if (completedTitles.Contains(title.Title.Name))
                title.Complete();

        // Title-set metas count completed members â€” after completions resolve, so
        // site-detected completions count too.
        foreach (var title in progress)
            if (title.PhoenixTitle is Phoenix2TitleSetTitle set)
                title.ApplyDirectProgress(progress.Count(p =>
                    set.CountsMember(p.PhoenixTitle) && p.IsComplete));

        return progress;
    }
}