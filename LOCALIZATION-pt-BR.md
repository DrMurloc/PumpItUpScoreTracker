# pt-BR localization glossary

Working reference for translating `App.en-US.resx` into `App.pt-BR.resx`. Captures the conventions already established by the existing 131 translated entries so future batches stay consistent.

For the localization mechanism itself (resx layout, `L["..."]` usage, key conventions), see [ARCHITECTURE.md](ARCHITECTURE.md#cross-cutting-concerns). For PIU domain terms in English, see [DOMAIN.md](DOMAIN.md).

## Style conventions

- **Address the user with informal `você`.** All existing prose uses `você` / `seu` / `sua`, never `tu` and never the formal `o senhor / a senhora`. Match that.
- **Sentence case in values, even when the English key is Title Case.** Key `"Add to Favorites"` → value `"Adicionar aos favoritos"`. Key `"Difficulty Level"` → value `"Nível de dificuldade"`. The English source preserves Title Case for searchability; pt-BR follows native sentence-case norms. Exception: brand-like terms (`Phoenix`, `PIUScores`, `Rastreador de Pontuações` for the app name itself) keep their internal capitalization.
- **Preserve positional placeholders verbatim.** `{0}`, `{1}`, `{2}` go into the value untouched, in whatever order Portuguese grammar wants. Example: key `"You are X place!"` (English value `"You are {0} Place!"`) → pt-BR value `"Você está em {0}!"` — placeholder kept, surrounding text rephrased.
- **Skip prose with inline markup.** Per CLAUDE.md, `<MudText>` bodies with embedded `<MudLink>`/other elements stay hardcoded English. Don't extract them, don't translate them.
- **Use proper Brazilian orthography.** Acute and grave accents, cedilla, tildes — `áéíóúâêôãõàç`. Don't ASCII-fold.

## Established term mappings

These have **at least one** existing translation in `App.pt-BR.resx`. New translations of the same term must reuse the established form unless there's a documented reason to change it.

### App / generic UI

| English | pt-BR | Notes |
|---|---|---|
| Score Tracker (the app) | Rastreador de Pontuações | Brand name. Title case. |
| Account | Conta | |
| About | Sobre | |
| Actions | Ações | |
| Average | Média | |
| Cancel | Cancelar | |
| Close | Fechar | |
| Completed | Concluído | |
| Date (recorded) | Data de registro | "Recorded Date" → "Data de registro" |
| Download | Baixar | verb. "Download Scores" → "Baixar pontuações" |
| Easy / Easier | Fácil / Mais fácil | |
| Easiest | Mais fácil | "Easiest Player" → "Jogador mais fácil" |
| Hard / Harder | Difícil / Mais difícil | |
| Hardest | Mais difícil | |
| Login (noun) | Login | Untranslated. |
| Log in (verb) | Fazer login | "Log In With {0}" → "Fazer login com {0}" |
| Logout | Fazer logout | |
| Language | Linguagem | |
| Medium | Médio | |
| Page (start/end) | Página inicial / Página final | |
| Place (rank/position) | Posição | Used as a leaderboard/ranking column header (more idiomatic than `Lugar` in this context). |
| Private / Public | Privado / Público | "Make Private" → "Tornar privado" |
| Restart | Reiniciar | |
| Save | Salvar | "Save Scores" → "Salvar pontuações" |
| Submit | Enviar | Standard pt-BR for form submit. |
| Text View | Visualização em texto | |
| Tools | Ferramentas | |
| Upload Image | Carregar imagem | Aligns with existing `carregado` usage. |
| Total | Total | |
| Upload (verb / noun) | Upload / Carregar | Mixed. "Upload Scores" → "Upload de pontuações"; "uploaded" → "carregado". Lean on context. |
| Username | Nome de usuário | |
| Very Easy / Very Hard | Muito fácil / Muito difícil | |
| Video | Vídeo | |
| Age | Idade | |
| Days Old | dias de idade | lowercase — used as suffix to a number ("42 dias de idade"). |
| Filters | Filtros | |
| Settings | Configurações | |
| All | Todos | Plural masculine. (When the underlying noun is feminine, use `Todas`; pick per key context.) |
| Category | Categoria | |
| Description | Descrição | |
| Level | Nível | "Min/Max Level" → "Nível mín./máx."; "Min/Max" alone → "Mín./Máx." |
| Median | Mediana | |
| Min / Max (bare) | Mín. / Máx. | Abbreviated. Used as standalone column or filter labels. |
| Overview | Visão geral | |
| Remaining | Restante | |
| Copied to clipboard! | Copiado para a área de transferência! | Matches the existing "Copy to Clipboard" → "Copiar para a área de transferência" verb form. |
| Communities | Comunidades | |
| Create | Criar | |
| Done | Concluído | Same as `Completed`. |
| Last Updated | Última atualização | |
| Maximum / Minimum (full word) | Máximo / Mínimo | Used when English source spells it out (e.g. "Maximum Level" → "Nível máximo"). Distinct from abbreviated `Min./Máx.` for `Min/Max` keys. |
| Minutes / Seconds | Minutos / Segundos | |
| Saved! | Salvo! | Snackbar success message. Masculine default. |

### PIU domain

| English | pt-BR | Notes |
|---|---|---|
| Chart(s) | chart / charts | **Untranslated**, lowercase even mid-phrase. "Chart Type" → "Tipo de chart". "Saved Charts" → "Charts salvos". |
| Charts List | Lista de charts | |
| Chart Randomizer | Sorteador de charts | |
| CoOp | CoOp | Untranslated. "CoOp Aggregation" → "Agregação de CoOp". |
| Singles / Doubles | Singles / Doubles | Untranslated. |
| Difficulty Level | Nível de dificuldade | |
| Letter Grade | Letras de nota | Min/Max forms use singular: "Min Letter Grade" → "Letra de nota mín.", "Max Letter Grade" → "Letra de nota máx." Old/New variants: "Old Letter Grade" → "Letra de nota antiga", "New Letter Grade" → "Letra de nota nova". |
| Letter-grade tier names (As, Ss, SSs, SSSs) | As, Ss, SSs, SSSs | **Untranslated**. Column-header labels for letter-grade counts ("how many A grades, how many S grades, …"). PIU jargon. |
| Mix | Versão | "Mix" → "Versão". XX, Phoenix proper names stay untranslated. |
| Phoenix | Phoenix | Game version proper name. Untranslated. |
| XX | XX | Game version proper name. Untranslated. |
| Plate | Plate | **Untranslated**. PIU community uses the English term in pt-BR contexts. |
| Pass (the verb / noun for completing a chart) | pass | **Untranslated**, lowercase. "Hide Completed Charts" → "Ocultar charts com pass". "Passed Count" → "Contagem de pass". "Not Passed Count" → "Contagem de músicas sem pass". Bare "Pass" / "Passes" → `pass` / `passes` (chart-series labels). Boolean column headers: "Passed" → `Com pass`, "Unpassed" → `Sem pass`. |
| Score (singular, generic) | Pontuação | "Score" → "Pontuação"; "Score State" → "Estado da pontuação". |
| Scores (plural / collection) | Pontuações | "Save Scores" → "Salvar pontuações"; "Official Scores" → "Pontuações oficiais"; "My Score" → "Minhas Pontuações" (intentionally plural in this entry). |
| Phoenix Score Calculator | Calculadora de pontuação da Phoenix | |
| Score Loss | Perda de pontuação | |
| Song | Música | "Song Name" → "Nome da música"; "Song Duration" → "Duração da música". |
| Song Artist | Artista da música | |
| Song Type | Tipo de música | |
| Title (in-game title award) | Título | "Title Progress" → "Progresso do título"; "Titles" → "Títulos". |
| To Do | Pendentes | "Add to ToDo" → "Adicionar à lista de tarefas"; "Show Only ToDo Charts" → `Mostrar somente charts "Pendentes"`. |
| Favorites | Favoritos | "Add to Favorites" → "Adicionar aos favoritos". |
| Tier Lists | Faixas de dificuldade | |
| Weekly Charts | Charts Semanais | `Charts` stays English (per the chart-untranslated rule); `Semanais` is the descriptive modifier. |
| Leaderboard | Classificação | |
| Players | Jogadores | |
| Tournaments | Campeonatos | |
| Popularity | Popularidade | |
| Progress | Progresso | "Progress Charts" → "Gráficos de progresso" (note: here `Charts` means graphs, not PIU charts — translated as `Gráficos`). |
| BPM | BPM | Untranslated. "Min BPM" → "BPM mín."; "Max BPM" → "BPM máx." |
| Difficulty Categorization | Categorização de dificuldade | |
| Note Count | Contagem de notas | Matches the "Contagem de pass" pattern. Min/Max: "Contagem mín./máx. de notas". |
| Personalized Difficulty | Dificuldade personalizada | |
| Player Count | Quantidade de jogadores | "Quantidade" reads more naturally than "Contagem" for the CoOp player-number numeric input. |
| PIU Tier List | Faixa oficial PIU | Page title; the official tier list provided by piugame.com. |
| Score Ranking | Ranking de pontuação | "Ranking" is a common pt-BR loanword. Distinct from `Leaderboard` → `Classificação`. |
| Scoring Level | Nível de pontuação | Matches the "Nível de dificuldade" pattern. |
| Skill | Habilidade | Chart trait/skill (runs, drills, twists, etc.). |
| Stage Pass | Pass do estágio | Tristate filter on chart-pass status. |
| Step Artist | Autor dos passos | Chart designer. Some pt-BR community usage leaves it in English; we translate. |
| Avg Plate | Plate média | `Plate` stays English (untranslated jargon); modifier translated. |
| Avg Score | Pontuação média | |
| Competitive Level | Nível competitivo | "Competitive Level: {0}" → "Nível competitivo: {0}". |
| Difficulty (bare noun) | Dificuldade | Distinct from `Difficulty Level` → `Nível de dificuldade`. |
| Pumbility | Pumbility | Untranslated. Proper-noun-style PIU concept. |
| Rating | Rating | **Untranslated**. Brazilian PIU community uses the English loanword (matches the `Pumbility`/`Plate`/`Bounty` policy). |
| Score Distribution | Distribuição de pontuação | "Show Score Distribution" → "Mostrar distribuição de pontuação". |
| Ungraded | Sem nota | Matches `sem pass` / `sem nota` family. "Not Graded Count" → "Contagem de músicas sem nota". |
| XX Progress | Progresso XX | No article — sidesteps the gender question for the legacy mix proper-noun. |
| Admin (role/area) | Admin | **Untranslated**. Role name. |
| Chart actions | Salvar / Atualizar / Criar chart | "Save Chart" → "Salvar chart"; "Update Chart" → "Atualizar chart"; "Chart saved" → "Chart salvo" (masculine — community treats `chart` as masc.). |
| Lifebar (one-word, the UI element) | lifebar | **Untranslated** when referring to the PIU UI element specifically. Bare `Life` translates as `Vida` (e.g. "Max Life" → "Vida máxima", "Life Threshold" → "Limite de vida"). Same split-treatment as `chart` (jargon untranslated) vs related Portuguese words. |
| Folder (PIU difficulty-level group) | folder | **Untranslated**. Brazilian PIU community uses both `folder` and `pasta`; we keep `folder` to match the loanword policy. E.g. "Folder Averages" → "Médias do folder"; "Weighted Average Scores By Folder" → "Pontuações médias ponderadas por folder". |
| Bounty / Bounties | Bounty / Bounties | **Untranslated**. PIU community concept. "Bounty Leaderboard" → "Classificação de bounties". |
| Anonymous | Anônimo | |
| Avatar | Avatar | **Untranslated**. Common loanword in pt-BR UI. |
| Calculated Tier List | Faixa calculada | Reuses the `Faixas de dificuldade` family. |
| Game Stats | Estatísticas do jogo | |
| User Matches / Match (vs another player) | Partidas do usuário / Partida | |
| Plates (plural) | Plates | **Untranslated**, plural form. "Plate Breakdown" → "Detalhamento de plates"; "Plate Distribution" → "Distribuição de plates". |
| Welcome (greeting) | Bem-vindo | Masculine default for the bare greeting. |
| Tournament / Campeonato (singular) | Campeonato | "Active Tournaments" → "Campeonatos ativos"; "Previous/Upcoming Tournaments" → "Campeonatos anteriores/próximos". |
| Qualifiers | qualifiers | **Untranslated**, lowercase. Brazilian tournament community uses both `qualifiers` and `qualificatórias`; we keep the loanword. E.g. "Qualifiers Leaderboard" → "Classificação dos qualifiers". |
| Brackets (tournament) | Chaves | **Translated**. Standard pt-BR for tournament brackets. "{0} Brackets" → "{0} Chaves". |
| Seed (tournament seeding) | Seed | **Untranslated**. Tournament jargon, common loanword. |
| Stamina | stamina | **Untranslated**. Brazilian PIU community usage. E.g. "Stamina Session Builder" → "Construtor de sessão de stamina". |
| Plays (PIU attempts) | plays | **Untranslated**, lowercase mid-phrase. "Play Count" → "Contagem de plays"; "{0} Plays" → "{0} plays". Same loanword treatment as `pass`/`run`/`chart`. |
| Pre-Score (compound prefix) | pré-pontuação | "Points Pre-Score" → "Pontos pré-pontuação". |
| Rankings (plural noun, generic) | Rankings | **Untranslated**, matches `Rating` loanword policy. "World Rankings" → "Rankings mundiais"; "Score Rankings" → "Rankings de pontuação". Distinct from `Leaderboard` → `Classificação`. |
| Tag / Tags | Tag / Tags | **Untranslated**. Brazilian Portuguese tech UI loanword. |
| Judgment terms (Bad / Miss / Perfect / Great / Good) | Bad / Miss / Perfect / Great / Good | **Untranslated**, including plural forms (`Bads`, `Misses`, `Greats`, `Perfects`, `Goods`). Brazilian PIU community uses the English judgment terms. |
| X Calculator | Calculadora de X | "Phoenix Score Calculator" → "Calculadora de pontuação da Phoenix" (existing); "Rating Calculator" → "Calculadora de rating"; "PIU Life Calculator" → "Calculadora de vida do PIU". |
| Delete | Excluir | Action verb. Distinct from `Remove from X` → `Remover de X` (existing). |

- **Possessives**: "Minhas Pontuações", "sua conta", "seu progresso" — possessive precedes noun, agrees in gender/number.
- **"With pass" / "without pass"**: phrased as `com pass` / `sem pass`. See "Hide Completed Charts" → "Ocultar charts com pass".
- **App self-reference**: "Rastreador de Pontuações" (the app's name in pt-BR). Used in disclaimers about account creation, password storage, etc.
- **Disclaimer voice**: existing privacy / make-public / make-private disclaimers use full sentences with `você`, dropped commas, and Portuguese conjunctions (`e`, `ou`, `mas`). Match that register — neither stiff nor slangy.
- **`Show X` toggles**: render as `Mostrar X` (lowercase X). E.g. "Show Age" → "Mostrar idade", "Show Skills" → "Mostrar habilidades", "Show Step Artist" → "Mostrar autor dos passos".
- **`Min/Max X` filter labels**: prefer postfixed abbreviation `X mín.` / `X máx.` over prefixed `Mín./Máx. X`. E.g. "Min BPM" → "BPM mín."; "Max Letter Grade" → "Letra de nota máx."
- **`(Data Backed)` parenthetical**: render as `(Baseado em dados)` / `(Baseada em dados)` — gender-agree with the preceding noun. E.g. "Pass (Data Backed)" → "Pass (Baseado em dados)" (`Pass` masculine); "Score (Data Backed)" → "Pontuação (Baseada em dados)" (`Pontuação` feminine).
- **`Old X` / `New X` pairs**: render as `X antigo(a)` / `X novo(a)` — gender-agree with the noun. E.g. "Old Letter Grade" → "Letra de nota antiga", "New Letter Grade" → "Letra de nota nova" (`Letra` feminine).
- **`Show X only` / `top` references**: "Show Top Only" → "Mostrar somente o topo" (literal). "Show Only ToDo Charts" → `Mostrar somente charts "Pendentes"` (established).
- **Tech loanwords**: `cache`, `hash`, `URL`, `Rating` stay untranslated — universal in pt-BR tech UI. E.g. "Clear Cache" → "Limpar cache"; "Youtube Hash" → "Hash do YouTube".
- **Decimal separator**: pt-BR uses comma — `9,6` not `9.6`. Localize numeric values inside translated prose.
- **Source typos in long-key prose**: when the English key has a clearly-broken sentence (e.g. missing word), translate the *intended* meaning so the pt-BR reads as correct grammar. Don't preserve the typo.
- **PIU community proper nouns / community jargon** in prose stay English: `run` (a play-through), `Rainbow` (lifebar color phase ≥1000), `data-mine` / `data-mining`, mix names (`NX2`, `Prime`, `XX`, `Phoenix`), and contributor names (`KyleTT`, `FEFEMZ`, `Team Infinitesimal`).
- **"To perfect" (verb — to achieve a Perfect grade)**: render as `tirar perfect`. E.g. "harder for higher level players to perfect" → "mais difíceis para jogadores de nível mais alto tirarem perfect". The community-jargon `perfectar` exists but is less standard.
- **`Is X` boolean column headers**: drop the `Is` prefix, render the noun/adjective alone. E.g. "Is Warmup" → "Aquecimento", "IsBroken" → "Quebrada". `É X` reads stilted as a column header.
- **Inline conjunctions composed via placeholders**: when English source uses tiny separately-keyed words (`are`, `are not`) that get composed into a sentence at runtime, translate each as the grammatical equivalent that fits the host sentence (`são` / `não são`). Verify by reading the host string with each value substituted.

## Open decisions (terms upcoming batches will need)

Pulled from the 442 missing keys. Each is a term that doesn't yet have an established pt-BR translation in the resx, and that future batches will hit. Decide once per term, then add the row to **Established term mappings** above and use it.

### PIU domain — high frequency

| English | Recommendation | Notes |
|---|---|---|
| Pumbility | **Pumbility** (untranslated) | Proper noun for PIU's composite player rating; community uses the English term. |
| UCS | **UCS** (untranslated) | Acronym (User-Created Step). Untranslated everywhere. |

### App / generic UI — high frequency

| English | Recommendation | Notes |
|---|---|---|
| Add | Adicionar | |

## Process for future batches

1. Pick a feature folder (Tournaments, Tier Lists, Progress, Admin, Tools, etc.).
2. List its English keys (`grep -oP '(?<=L\[")[^"]+' ScoreTracker/ScoreTracker/Pages/<Folder>/**/*.razor` or similar).
3. Cross-reference against `App.pt-BR.resx` to find which are missing.
4. Translate using this glossary. **If a new term needs a decision, add a row to "Established term mappings" before translating.**
5. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.
6. PR titled like `Translate <Folder> to pt-BR (Phase N)`.
