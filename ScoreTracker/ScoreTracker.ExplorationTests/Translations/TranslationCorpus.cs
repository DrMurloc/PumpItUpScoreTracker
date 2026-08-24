namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     One comment to translate, and what must be true of every rendering of it. This is the
///     deterministic half of the evaluation: checked in code, costing nothing, and separating the
///     model tiers without anybody's opinion being involved.
///     <para>
///         The two classes are not the same rule, and collapsing them would produce false
///         failures. A difficulty code, a score, a timestamp, and an emoji are the same glyphs in
///         Korean as in Spanish. A person's or chart's name is not: Korean legitimately writes
///         Fefemz as 피펨즈, so demanding the Latin spelling there would mark a correct rendering
///         wrong. Country names are in neither class — México becoming Mexique is the translation
///         working, not failing.
///     </para>
/// </summary>
/// <param name="MustSurviveEverywhere">Verbatim in all four renderings, Korean included.</param>
/// <param name="NamesInLatinScript">
///     Verbatim in es-ES, fr-FR, and pt-BR. Korean is reported but not asserted, because
///     transliteration into Hangul is the right answer there.
/// </param>
internal sealed record CorpusComment(
    string Id,
    string Text,
    string ExpectedLanguage,
    string Note,
    string[] MustSurviveEverywhere,
    string[] NamesInLatinScript);

/// <summary>
///     Real Pump It Up community text, collected by the owner. Nothing is invented, because
///     invented comments are tidier than real ones in exactly the ways that hide translation
///     failures.
///     <para>
///         Three sources, deliberately: YouTube reaction comments on chart videos, Discord
///         conversation, and one official Andamiro announcement. The announcement is the only
///         formal register here and the only text carrying a URL. The Discord English is the
///         closest thing to what a chart-comments feature would actually hold — two players
///         working out how to pass a chart — so it is the sample the recommendation should be
///         read against.
///     </para>
///     <para>
///         The distribution is what the community actually produces: English, Korean, and Spanish,
///         with one Portuguese comment. There is no French source, so fr-FR is exercised only as
///         an output. The Spanish is Latin American — Mexican, Argentine, Chilean — while the
///         target is peninsular es-ES, which is a real and deliberate mismatch rather than an
///         oversight.
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
            ["D29"], ["EXC"]),

        new CorpusComment("DDX", "FATALITY", "en",
            "One word, all caps, a Mortal Kombat reference. Nothing to translate and everything to get wrong.",
            [], []),

        new CorpusComment("user-b261cen3p", "It doesn't have spin which is in V3 D9 D8 confirmed", "en",
            "Barely grammatical. The pivot must not tidy it up, and the meaning has to survive anyway.",
            ["D9", "D8"], ["V3"]),

        new CorpusComment("jaredteewj", "God bless u fefemz", "en",
            "A player name in lowercase, plus SMS spelling. ko-KR should reach 피펨즈.",
            [], ["fefemz"]),

        new CorpusComment("michaelstarlight", "we're cooked guys", "en",
            "Current slang. A literal rendering produces cooking.",
            [], []),

        new CorpusComment("EXC_Follwer",
            "진짜 너무 잘만들었고 공을 엄청 들인게 보이는데.. 이거 밟다 다치지나 않았으면 좋겠네요.. 피펨즈님..화이팅..",
            "ko",
            "해요체, warm and concerned. 피펨즈님 carries the honorific; 화이팅 is encouragement with no clean English equivalent.",
            [], []),

        new CorpusComment("passerby-s9e", "추가적으로 잊으시면 안됩니다. BPM 240이라는걸....", "ko",
            "합쇼체, formal. The trailing ellipsis is doing tonal work.",
            ["240"], []),

        new CorpusComment("AwesomeH69",
            """
            2:01 When the stepzone splits into normal P1 and P2 versus, and casuals are waiting to play:

            "You can go now"
            """,
            "en",
            "A meme format: timestamp, setup, quoted punchline. Flatten the structure and the joke dies.",
            ["2:01", "P1", "P2"], []),

        new CorpusComment("user-oc5rj6fn8c", "명백하게 d9입니다. 유포리아닉 d10보다 약간 쉬운 것 같네요", "ko",
            "합쇼체. Lowercase d9/d10 must not be normalised, and 유포리아닉 is a song title the glossary does not list.",
            ["d9", "d10"], []),

        new CorpusComment("SkyShine_", "진짜 궁금해서 그러는데 이게 재밌음? 일단 보는건 어이없어서 재밌다야", "ko",
            "반말 with the -음 ending. Genuinely casual, mildly dismissive.",
            [], []),

        new CorpusComment("J4son_xXx", "최권식형 돌아와줘", "ko",
            "반말 plus 형 (older-brother address). A Korean personal name and a kinship term English has no word for.",
            [], []),

        new CorpusComment("spartan7919",
            "이걸 통과해줄 정도면 검수는 그냥 우덜식 친목으로 하는거임? 짠 사람 보다 무리배치 안 쳐내고 그대로 선보이는... 검수 이 따위로하는 그 사람은 참 대단하네요",
            "ko",
            "The hardest case here. 대단하네요 is sarcastic — read straight it becomes sincere praise and inverts the comment.",
            [], []),

        new CorpusComment("gogogo587",
            "그냥 5놋 개구리 해라는거네 진짜 펌프 역사상 역대급 개 뇌절 관종 채보다 ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ 유저들 욕해도 오락실에서 개굴 개굴 하면서 하는거 생각하니 개웃기넼ㅋㅋㅋㅋㅋㅋㅋㅋ",
            "ko",
            "Dense slang: 뇌절, 관종, 개- intensifiers, frog noises, and two runs of ㅋ. The laughter should land as jajaja / kkkkk / mdr without the prompt ever mentioning it.",
            [], []),

        new CorpusComment("nicolecataldoata",
            "Grande franco! Nos dejaste con ataque cardiaco con ese tremendo combo. Aveces se gana, aveces se pierde, lo mas importante, es las lecciones que puedes sacar de ambas. Admiro tu constancia, humildad y talento, vo dale wee",
            "es",
            "218 chars. Missing accents throughout, and 'vo dale wee' is Chilean — neither es-ES nor es-MX covers it.",
            [], ["Franco"]),

        new CorpusComment("darlenec2663",
            "No manches, y yo apenas que puedo con un nivel 5! que talento jaja espero algún día llegar a ser tan buena como ustedes <3",
            "es",
            "Mexican 'no manches', 'ustedes' for plural address, jaja, and an emoticon. es-ES should reach vosotros.",
            [], []),

        new CorpusComment("wdgaster7990",
            "Juegas exelente carnal , el problema fue a la mitad de la canción casi llegando al final , te abandono la fuerza , se siente una gran impotencia cuando pasa eso , pero no te preocupes ,son cosas que pasan , bien jugado a los dos y sigue dando el 1000% como lo has hecho siempre , un saludo desde México.",
            "es",
            "~300 chars, would have been truncated at 200. 'carnal' is very casual Mexican; the spaced-out commas are the author's own.",
            ["1000%"], []),

        new CorpusComment("joaobatistamotta3888",
            "Parabéns que partida incrível, zafrada acompanho seus treinos e vi seu potencial e resistência mas esse Franco poucas partida em que vi ele atuando, surpreende e muito, mas a resistência é sua dificuldade, combo maravilhoso, os dois estão de parabéns quero ver vocês fazerem bonito na big one, até lá.",
            "pt",
            "The only Portuguese source. Run-on and ungrammatical in places; 'big one' is the event, lowercased.",
            [], ["Franco"]),

        new CorpusComment("juancarlosgonzalezespiritu5896",
            "Amigos alguien representara a México en este torneo 🤔", "es",
            "An emoji doing the work of a question mark, and a missing accent on representará.",
            ["🤔"], []),

        new CorpusComment("axelmigueles3949",
            "Es un asco que griten como locos... Eso afecta mucho en la distracción de los 2 ... Si se callan van a estar al 100 💯 .. pelotudos",
            "es",
            "Argentine 'pelotudos' — heated but aimed at crowd noise, not a person's identity. An emoji stands in for a word.",
            ["💯"], []),

        new CorpusComment("danx021", "Zafada es aburrido no es nada al lado de Fefemz", "es",
            "A chart name and a player name in one short comparison.",
            [], ["Zafada", "Fefemz"]),

        new CorpusComment("theYTfox", "Va a estar potente el Franco para este B1G, rival durisimo a vencer.", "es",
            "B1G is the event abbreviation; another comment writes it 'big one'.",
            [], ["B1G", "Franco"]),

        new CorpusComment("lunanunna", "want to watch QUATTUORUX D26 989,999", "en",
            "English from a Korean-handle account — source language is per comment, not per user. The score keeps its comma.",
            ["D26", "989,999"], ["QUATTUORUX"]),

        new CorpusComment("Volandoconlosmays",
            """
            De todos los jugadores existentes actualmente, tú eres el más completo para mí.
            Ojalá pueda conocerte en el Big One, fuiste mucha inspiración para mí para retomar esto que había dejado en el olvido.
            Hoy ya tengo mi propio tablero en casa y seguimos en esto.
            Gracias. Ver tus logros es inspirador y hace pensar que también puedo seguir subiendo de nivel.
            """,
            "es",
            "~330 chars over four lines — the longest and warmest in the corpus. Uses tú. Line breaks should survive.",
            [], ["Big One"]),

        // --- Official announcement (Andamiro, Korean) ---------------------------------
        // Corporate and legal register, which nothing else in the corpus reaches, plus a URL.

        new CorpusComment("andamiro-notice",
            "안녕하세요. 안다미로 관리자입니다. 어제 CookieRun: Braverse의 공식 SNS X(전 트위터)에서 펌프잇업 불법 프로그램을 사용한 오프라인 이벤트 글이 게시되었습니다. 이를 본 유저분들이 제보를 해주셨고 담당자와 직접 연결하여 해당 이벤트는 즉각 취소되었음을 알려드립니다. https://twitter.com/CRbraverse/status/1711458954081722605",
            "ko",
            "Official notice, 합쇼체 throughout. Carries a live URL — the one entity a prompt-injection check would care about — plus two company names and a game title.",
            ["https://twitter.com/CRbraverse/status/1711458954081722605"],
            ["CookieRun", "Braverse"]),

        new CorpusComment("andamiro-trademark",
            "안다미로는 펌프잇업 상표 및 펌프잇업 게임에 대한 모든 권한을 가지고 있으며, 안다미로의 승인없이 펌프잇업을 모방하거나 펌프잇업의 리소스를 사용한 모든 제작물은 상업적으로 이용할 수 없습니다. 개발팀에서는 펌프잇업 게임 리소스를 사용하여 제작된 불법 프로그램의 존재를 알고 있습니다.",
            "ko",
            "Legal prose — a trademark assertion. Register is as far from a chart comment as this corpus goes, and 안다미로 / 펌프잇업 must reach Andamiro / Pump It Up in Latin script.",
            [], ["Andamiro"]),

        // --- Discord, Spanish: buying used cabinets ------------------------------------

        new CorpusComment("conjo-usadas",
            "como a cuanto estás comprando esas versiones usadas? poco después de que salga phoenix 2 la 1 debería estar bastante más barata que ahora",
            "es",
            "Lowercase throughout, no opening inverted question mark. The elliptical la 1 means Phoenix 1 and only resolves from context.",
            [], ["phoenix 2"]),

        new CorpusComment("kaimaruz-precios",
            """
            La PHX 1 ahorita no baja de 15K
            La XX la conseguí en 4mil
            Igual no me afecta mucho perderme la 2, la Phoenix 1 solo la jugué un par de veces hahaha así que tendría toda una versión nueva que explorar
            """,
            "es",
            "Prices in two different shorthands (15K, 4mil), ahorita is Mexican, and hahaha should localize per target.",
            // 15K is an international shorthand and must survive; 4mil is Spanish for "four
            // thousand" and legitimately becomes 4 000, 4.000 or 4k elsewhere — asserting it
            // verbatim was the Mexico/Mexique mistake again, and marked all three arms wrong
            // for localizing a number correctly.
            ["15K"], ["PHX", "XX"]),

        new CorpusComment("kaimaruz-local",
            "Igual con XX me perdí casi todo lo que salió de 2021 para adelante porque ahí fue cuando cerraron el local y el más cercano me queda a 2 horas de viaje y tristemente ese tampoco tendrá Phoenix 2 por el precio xD",
            "es",
            "A run-on with real sadness under an xD. A year, a travel time, and an emoticon doing tonal work.",
            ["2021"], ["XX", "Phoenix 2"]),

        new CorpusComment("alvar-salto",
            """
            creeme
            el salto ente prime2 e phoenix1 es enorme
            hay como el doble de canciones entre esas dos
            phoenix1 tiene como 150 nuevas canciones
            y en general el nuevo tema de puntuaciones que van hasta un millon y los rerates y cosas estan muy bien
            así que si, mejor compra phoenix1 y se feliz
            """,
            "es",
            "Six lines, no accents, two typos (ente for entre, e for y). rerates is untranslated English jargon sitting inside Spanish — a test of leaving a borrowed term alone.",
            ["150"], ["prime2", "phoenix1"]),

        // --- Discord, Spanish: release timing and sanctions ----------------------------

        new CorpusComment("xtrem3x-kits",
            """
            acá no habrá evento, solo anunciarán que están listos los kits y todos irán por ello
            pero seguro será hasta precisamente el lunes 20 o muy tardado el miercoles 22
            así que desconozco como le hicieron para tener la versión antes que todos los demas ... ya si los de AM consideran que recurrieron a una falta pues posiblemente les bloqueen los discos
            """,
            "es",
            "AM is Andamiro abbreviated — an initialism the glossary does not list. Weekday-plus-number dates must not be reformatted.",
            ["20", "22"], ["AM"]),

        new CorpusComment("kaimaruz-funa",
            """
            Sin miedo a la funa alguien de acá sabe que dicen los textos detrás de la mona China verde?
            Yo nomás leo Infinity y Resurrection xd
            """,
            "es",
            "funa is Chilean and Argentine for a public shaming — regional slang neither es-ES nor es-MX uses. mona China verde is colloquial for a character on a song jacket.",
            // xd is laughter, not an identifier: it correctly becomes ㅋㅋ in Korean and kkkk
            // in Portuguese. Asserting it verbatim contradicted the design rule it was meant
            // to protect — the same mistake as 4mil, and as Mexico/Mexique before it.
            [], ["Infinity", "Resurrection"]),

        // --- Discord, English: the tips content the real feature would carry -----------

        new CorpusComment("touhoufan-thug",
            """
            How do you thug out Skeptic 22?
            Is it just a form thing or do I really need to exercise more?
            """,
            "en",
            "The closest thing here to a real chart tip. thug out is community jargon for muscling through a chart; a literal rendering produces criminals.",
            ["22"], ["Skeptic"]),

        new CorpusComment("alex-runs",
            """
            What part are you struggling on
            Just overall 200bpm runs or the weird tech part before the run
            """,
            "en",
            "run and tech are PIU pattern vocabulary, deliberately absent from the glossary. Spanish must not reach carrera (racing) nor French course.",
            ["200"], []),

        new CorpusComment("touhoufan-dement",
            "It's destroying me like Dement s21. It feels awkward",
            "en",
            "s21 is Singles 21 in lowercase shorthand, and Dement is a song title that reads like an English word.",
            ["s21"], ["Dement"]),

        // The link-marker pair (2026-08-24). The production pipeline lifts URLs out before the
        // model sees the text, leaving markers like ⟦1⟧ the prompts describe as links — so these
        // two are the corpus as production would actually submit it. Mid-sentence on purpose:
        // grammar has to wrap the marker (a Korean particle would attach to it, Spanish wants an
        // article), which is exactly where a marker could get absorbed, doubled, or dropped.
        // Synthetic where everything above is collected — production texts with markers cannot
        // exist before the feature ships, and the shape under test is the marker, not the prose.
        new CorpusComment("marker-korean",
            "⟦1⟧ 이 영상 2:01 부분 보면 발 바꾸는 타이밍 나와요. ⟦2⟧ 채보도 참고하세요",
            "ko",
            "Two markers, one leading a sentence where Korean wants a particle on it. Both must ride through the pivot and every rendering untouched.",
            ["⟦1⟧", "⟦2⟧", "2:01"], []),

        new CorpusComment("marker-spanish",
            "miren el run en ⟦1⟧ antes de intentarlo, casi nadie lo pasa a la primera",
            "es",
            "One marker mid-sentence where Spanish reaches for an article. The rendering keeps the marker bare — never el ⟦1⟧ becoming a mangled token — and invents no URL.",
            ["⟦1⟧"], [])
    };
}
