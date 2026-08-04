namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     One comment to translate, and what must be true of every rendering of it.
///     <para>
///         <paramref name="MustSurvive" /> is the deterministic half of the evaluation: names,
///         difficulty codes, scores, and timestamps that have to appear verbatim in all four
///         outputs. It is checked in code, costs nothing, and separates the model tiers without
///         anybody's opinion being involved.
///     </para>
/// </summary>
internal sealed record CorpusComment(
    string Id,
    string Text,
    string ExpectedLanguage,
    string Note,
    params string[] MustSurvive);

/// <summary>
///     Real Pump It Up comments, taken from YouTube by the owner. Everything here was written by
///     a player about a chart or a match — nothing is invented, because invented comments are
///     tidier than real ones in exactly the ways that hide translation failures.
///     <para>
///         The distribution is what the community actually produces: English, Korean, and Spanish,
///         with one Portuguese comment. There is no French source, so fr-FR is exercised only as
///         an output. The Spanish is Latin American — Mexican and Argentine — while the target is
///         peninsular es-ES, which is a real and deliberate mismatch rather than an oversight.
///     </para>
///     <para>
///         Four comments exceed 200 characters, which is why the cap is 500: the longest ones are
///         the heartfelt replies, and a limit that cut them would discard the comments most worth
///         translating while keeping "FATALITY".
///     </para>
/// </summary>
internal static class TranslationCorpus
{
    public static readonly IReadOnlyList<CorpusComment> All = new[]
    {
        new CorpusComment("BOSS_PIUVN", "You can't beat EXC D29", "en",
            "Short, entity-dense. A difficulty code and a chart abbreviation in six words.",
            "EXC", "D29"),

        new CorpusComment("DDX", "FATALITY", "en",
            "One word, all caps, a Mortal Kombat reference. Nothing to translate and everything to get wrong."),

        new CorpusComment("user-b261cen3p", "It doesn't have spin which is in V3 D9 D8 confirmed", "en",
            "Barely grammatical. The pivot must not tidy it up, and the meaning has to survive anyway.",
            "V3", "D9", "D8"),

        new CorpusComment("jaredteewj", "God bless u fefemz", "en",
            "A player name in lowercase, plus SMS spelling. ko-KR should reach 피펨즈.",
            "fefemz"),

        new CorpusComment("michaelstarlight", "we're cooked guys", "en",
            "Current slang. A literal rendering produces cooking."),

        new CorpusComment("EXC_Follwer",
            "진짜 너무 잘만들었고 공을 엄청 들인게 보이는데.. 이거 밟다 다치지나 않았으면 좋겠네요.. 피펨즈님..화이팅..",
            "ko",
            "해요체, warm and concerned. 피펨즈님 carries the honorific; 화이팅 is encouragement with no clean English equivalent.",
            "피펨즈"),

        new CorpusComment("passerby-s9e", "추가적으로 잊으시면 안됩니다. BPM 240이라는걸....", "ko",
            "합쇼체, formal. Trailing ellipsis is doing tonal work.",
            "240"),

        new CorpusComment("AwesomeH69",
            """
            2:01 When the stepzone splits into normal P1 and P2 versus, and casuals are waiting to play:

            "You can go now"
            """,
            "en",
            "A meme format: timestamp, setup, quoted punchline. Flatten the structure and the joke dies.",
            "2:01", "P1", "P2"),

        new CorpusComment("user-oc5rj6fn8c", "명백하게 d9입니다. 유포리아닉 d10보다 약간 쉬운 것 같네요", "ko",
            "합쇼체. Lowercase d9/d10 must not be normalised, and 유포리아닉 is a song title the glossary does not list.",
            "d9", "d10"),

        new CorpusComment("SkyShine_", "진짜 궁금해서 그러는데 이게 재밌음? 일단 보는건 어이없어서 재밌다야", "ko",
            "반말 with the -음 ending. Genuinely casual, mildly dismissive."),

        new CorpusComment("J4son_xXx", "최권식형 돌아와줘", "ko",
            "반말 plus 형 (older-brother address). A Korean personal name and a kinship term English has no word for."),

        new CorpusComment("spartan7919",
            "이걸 통과해줄 정도면 검수는 그냥 우덜식 친목으로 하는거임? 짠 사람 보다 무리배치 안 쳐내고 그대로 선보이는... 검수 이 따위로하는 그 사람은 참 대단하네요",
            "ko",
            "The hardest case here. 대단하네요 is sarcastic — read straight it becomes sincere praise and inverts the comment."),

        new CorpusComment("gogogo587",
            "그냥 5놋 개구리 해라는거네 진짜 펌프 역사상 역대급 개 뇌절 관종 채보다 ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ 유저들 욕해도 오락실에서 개굴 개굴 하면서 하는거 생각하니 개웃기넼ㅋㅋㅋㅋㅋㅋㅋㅋ",
            "ko",
            "Dense slang: 뇌절, 관종, 개- intensifiers, frog noises, and two runs of ㅋ. The laughter should land as jajaja / kkkkk / mdr without the prompt ever mentioning it."),

        new CorpusComment("nicolecataldoata",
            "Grande franco! Nos dejaste con ataque cardiaco con ese tremendo combo. Aveces se gana, aveces se pierde, lo mas importante, es las lecciones que puedes sacar de ambas. Admiro tu constancia, humildad y talento, vo dale wee",
            "es",
            "218 chars. Missing accents throughout, and 'vo dale wee' is Chilean — neither es-ES nor es-MX covers it.",
            "franco"),

        new CorpusComment("darlenec2663",
            "No manches, y yo apenas que puedo con un nivel 5! que talento jaja espero algún día llegar a ser tan buena como ustedes <3",
            "es",
            "Mexican 'no manches', 'ustedes' for plural address, jaja, and an emoticon. es-ES should reach vosotros."),

        new CorpusComment("wdgaster7990",
            "Juegas exelente carnal , el problema fue a la mitad de la canción casi llegando al final , te abandono la fuerza , se siente una gran impotencia cuando pasa eso , pero no te preocupes ,son cosas que pasan , bien jugado a los dos y sigue dando el 1000% como lo has hecho siempre , un saludo desde México.",
            "es",
            "~300 chars, would have been truncated at 200. 'carnal' is very casual Mexican; spaced-out commas are the author's own.",
            "1000%", "México"),

        new CorpusComment("joaobatistamotta3888",
            "Parabéns que partida incrível, zafrada acompanho seus treinos e vi seu potencial e resistência mas esse Franco poucas partida em que vi ele atuando, surpreende e muito, mas a resistência é sua dificuldade, combo maravilhoso, os dois estão de parabéns quero ver vocês fazerem bonito na big one, até lá.",
            "pt",
            "The only Portuguese source. Run-on and ungrammatical in places; 'big one' is the event, lowercased.",
            "Franco"),

        new CorpusComment("juancarlosgonzalezespiritu5896",
            "Amigos alguien representara a México en este torneo 🤔", "es",
            "An emoji doing the work of a question mark, and a missing accent on representará.",
            "México", "🤔"),

        new CorpusComment("axelmigueles3949",
            "Es un asco que griten como locos... Eso afecta mucho en la distracción de los 2 ... Si se callan van a estar al 100 💯 .. pelotudos",
            "es",
            "Argentine 'pelotudos' — heated but aimed at crowd noise, not a person's identity. An emoji stands in for a word.",
            "💯"),

        new CorpusComment("danx021", "Zafada es aburrido no es nada al lado de Fefemz", "es",
            "A chart name and a player name in one short comparison.",
            "Zafada", "Fefemz"),

        new CorpusComment("theYTfox", "Va a estar potente el Franco para este B1G, rival durisimo a vencer.", "es",
            "B1G is the event abbreviation; another comment writes it 'big one'.",
            "B1G", "Franco"),

        new CorpusComment("lunanunna", "want to watch QUATTUORUX D26 989,999", "en",
            "English from a Korean-handle account — source language is per comment, not per user. The score keeps its comma.",
            "QUATTUORUX", "D26", "989,999"),

        new CorpusComment("Volandoconlosmays",
            """
            De todos los jugadores existentes actualmente, tú eres el más completo para mí.
            Ojalá pueda conocerte en el Big One, fuiste mucha inspiración para mí para retomar esto que había dejado en el olvido.
            Hoy ya tengo mi propio tablero en casa y seguimos en esto.
            Gracias. Ver tus logros es inspirador y hace pensar que también puedo seguir subiendo de nivel.
            """,
            "es",
            "~330 chars over four lines — the longest and warmest in the corpus. Uses tú. Line breaks should survive.",
            "Big One")
    };
}
