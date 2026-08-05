namespace ScoreTracker.Translations.Domain;

/// <summary>
///     The terminology a model cannot know, and nothing else.
///     <para>
///         The line is deliberate. A model already knows that Korean ㅋㅋㅋ is jajaja in Spanish
///         and kkkkk in Brazilian Portuguese, that "no manches" is casual Mexican, and how to
///         render a Korean speech level. Teaching it any of that costs tokens for nothing —
///         and worse, it destroys the measurement: whatever the prompt supplies, every model
///         tier gets right, so the cheap tier stops being distinguishable from the expensive one
///         exactly where it matters. Anything the model should already know belongs in the
///         evaluation, not here.
///     </para>
///     <para>
///         What survives that cut is three kinds of thing: house decisions the volunteers made
///         where another rendering was equally defensible (this codebase says Mix → 시리즈, not
///         믹스), community proper nouns that exist nowhere else (피펨즈 is Fefemz), and format
///         constraints that are rules rather than knowledge (a difficulty code is never
///         translated and never reformatted).
///     </para>
///     <para>
///         Sourced from <c>docs/LOCALIZATION-&lt;locale&gt;.md</c>. Only rows plausible in a player's
///         comment are carried; UI chrome like "Add to Favorites" is left behind. The song and
///         player tables are hand-seeded from the corpus for now — the site already stores
///         per-culture song names and game tags, so generating them is the next step, not
///         another authoring job.
///     </para>
/// </summary>
internal static class TranslationGlossary
{
    /// <summary>
    ///     Rendered into both prompts. Blank cells are deliberate: the locale has no house
    ///     rendering, and the model should use its own judgement rather than be handed a guess.
    /// </summary>
    public const string Text =
        """
        ## Terms this community renders a specific way

        Where a cell is blank there is no house rendering — use your own judgement.
        Anything absent from this table is your call too; the table is short on purpose.

        | English | es-ES | fr-FR | ko-KR | pt-BR |
        |---|---|---|---|---|
        | chart | chart (EN, masculine) | Chart (EN, feminine) | 채보 | chart (EN, lowercase) |
        | Mix (a game version) | versión | Mix | 시리즈 | Versão |
        | score | score (EN, masculine) | Score | 점수 | pontuação |
        | pass (verb, to clear) | pasar | Pass (EN) | 클리어하다 | dar pass |
        | pass (noun) | pass (EN, masculine) | Pass (EN) | 성공 | pass (EN) |
        | break / broken | break / broken (EN) | Cassé | 브레이크 오프 | |
        | plate | plate (EN, masculine) | Plaque | 플레이트 | Plate (EN) |
        | letter grade | nota | Rang (lettres) | 랭크 | letra de nota |
        | rating | rating (EN, masculine) | Rating (EN) | 레이팅 | Rating (EN) |
        | Pumbility | Pumbility | Pumbility | Pumbility | Pumbility |
        | Singles / Doubles | Singles / Doubles (EN) | Singles / Doubles (EN) | 싱글 / 더블 | Singles / Doubles (EN) |
        | CoOp | CoOp (EN) | CoOp (EN) | 코옵 | CoOp (EN) |
        | tier list | tier list (EN, feminine) | Tier List (EN) | 서열표 | faixa de dificuldade |
        | leaderboard | leaderboard (EN, masculine) | Leaderboard (EN) | 리더보드 | classificação |
        | rankings | ranking(s) (EN) | classements | 랭킹 | ranking |
        | tournament | torneo | tournoi | 대회 | campeonato |
        | qualifiers | clasificatorias | qualifications | 예선과제 | |
        | step artist | step artist (EN) | Step Artist (EN) | | autor dos passos |
        | skill (chart trait) | skill (EN, feminine) | compétence | | habilidade |
        | song | canción | chanson | 노래 | música |
        | player | jugador | joueur | 유저 | jogador |
        | level | nivel | niveau | 난이도 | nível |
        | run (a stretch of fast consecutive steps) | run (EN) | run (EN) | | run (EN) |
        | tech (technical, twisty patterns) | tech (EN) | tech (EN) | | tech (EN) |
        | one attempt at a chart | intento | | | |
        | lifebar / life | barra de vida / vida | | | |
        | Perfect / Great / Good / Bad / Miss | English, capitalized | | | |
        | Phoenix, XX (game versions) | unchanged | unchanged | unchanged | unchanged |
        | BPM | BPM | BPM | BPM | BPM |

        ## Community proper nouns

        | Canonical | Also written | Notes |
        |---|---|---|
        | Fefemz | 피펨즈 (ko), fefemz | A player. Korean comments use the Hangul transliteration. |
        | Big One | B1G | A tournament. Both surface forms name the same event. |

        ## Formats that are never translated and never reformatted

        - Difficulty codes: `D29`, `S17`, `D9`, `CoOp3`. The letter is the chart type and the
          number is its level; both stay exactly as written, in Latin script and Arabic numerals.
        - Scores: `989,999` keeps its digits and its separators.
        - Timestamps: `2:01` is a position in a video, not a duration to localize.
        - Song and chart titles, unless this glossary lists a rendering for that locale.
        - A number after a game name is a **version**, never a difficulty: `Phoenix 2`, `PHX 1`,
          `prime2`, `XX`. Rendering "PHX 1" as "Phoenix level 1" says something the author did not.
        """;

    /// <summary>
    ///     The one thing the source documents get wrong for this job. The UI catalogues are
    ///     written in a fixed house voice — fr-FR addresses the reader as <c>vous</c> on every
    ///     string — and a model handed terminology drawn from them may infer the voice along with
    ///     the vocabulary. A comment's voice belongs to whoever wrote it.
    /// </summary>
    public const string RegisterCaveat =
        """
        The terminology above comes from this site's interface catalogues, which are written in a
        fixed house voice. Take the vocabulary and ignore the voice: a comment's register belongs
        to the person who wrote the comment, never to the glossary.
        """;
}
