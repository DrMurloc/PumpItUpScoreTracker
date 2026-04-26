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

### PIU domain

| English | pt-BR | Notes |
|---|---|---|
| Chart(s) | chart / charts | **Untranslated**, lowercase even mid-phrase. "Chart Type" → "Tipo de chart". "Saved Charts" → "Charts salvos". |
| Charts List | Lista de charts | |
| Chart Randomizer | Sorteador de charts | |
| CoOp | CoOp | Untranslated. "CoOp Aggregation" → "Agregação de CoOp". |
| Singles / Doubles | Singles / Doubles | Untranslated. |
| Difficulty Level | Nível de dificuldade | |
| Letter Grade | Letras de nota | Min/Max forms use singular: "Min Letter Grade" → "Letra de nota mín.", "Max Letter Grade" → "Letra de nota máx." |
| Mix | Versão | "Mix" → "Versão". XX, Phoenix proper names stay untranslated. |
| Phoenix | Phoenix | Game version proper name. Untranslated. |
| XX | XX | Game version proper name. Untranslated. |
| Plate | Plate | **Untranslated**. PIU community uses the English term in pt-BR contexts. |
| Pass (the verb / noun for completing a chart) | pass | **Untranslated**, lowercase. "Hide Completed Charts" → "Ocultar charts com pass". "Passed Count" → "Contagem de pass". "Not Passed Count" → "Contagem de músicas sem pass". |
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

### Phrasing patterns to copy

- **Possessives**: "Minhas Pontuações", "sua conta", "seu progresso" — possessive precedes noun, agrees in gender/number.
- **"With pass" / "without pass"**: phrased as `com pass` / `sem pass`. See "Hide Completed Charts" → "Ocultar charts com pass".
- **App self-reference**: "Rastreador de Pontuações" (the app's name in pt-BR). Used in disclaimers about account creation, password storage, etc.
- **Disclaimer voice**: existing privacy / make-public / make-private disclaimers use full sentences with `você`, dropped commas, and Portuguese conjunctions (`e`, `ou`, `mas`). Match that register — neither stiff nor slangy.
- **`Show X` toggles**: render as `Mostrar X` (lowercase X). E.g. "Show Age" → "Mostrar idade", "Show Skills" → "Mostrar habilidades", "Show Step Artist" → "Mostrar autor dos passos".
- **`Min/Max X` filter labels**: prefer postfixed abbreviation `X mín.` / `X máx.` over prefixed `Mín./Máx. X`. E.g. "Min BPM" → "BPM mín."; "Max Letter Grade" → "Letra de nota máx."
- **`(Data Backed)` parenthetical**: render as `(Baseado em dados)` / `(Baseada em dados)` — gender-agree with the preceding noun. E.g. "Pass (Data Backed)" → "Pass (Baseado em dados)" (`Pass` masculine); "Score (Data Backed)" → "Pontuação (Baseada em dados)" (`Pontuação` feminine).

## Open decisions (terms upcoming batches will need)

Pulled from the 442 missing keys. Each is a term that doesn't yet have an established pt-BR translation in the resx, and that future batches will hit. Decide once per term, then add the row to **Established term mappings** above and use it.

### PIU domain — high frequency

| English | Recommendation | Notes |
|---|---|---|
| Bounty | **Bounty** (untranslated) | Niche PIU community concept; matches the pattern of leaving `Plate`, `pass`, `chart` untranslated. Confirms with Tier List and Bounty being parallel features. |
| Pumbility | **Pumbility** (untranslated) | Proper noun for PIU's composite player rating; community uses the English term. |
| UCS | **UCS** (untranslated) | Acronym (User-Created Step). Untranslated everywhere. |
| Community / Communities | Comunidade / Comunidades | Direct cognate. |
| Avatar | Avatar | Untranslated, common loanword in pt-BR UI. |
| Tournament (singular) | Campeonato | Plural already established as `Campeonatos`. |
| Match (versus another player) | Partida | Standard pt-BR for game match. (`User Matches` → `Partidas do usuário`, etc.) |
| Stats | Estatísticas | |
| Game Stats | Estatísticas do jogo | |
| Difficulty | Dificuldade | Bare noun. |
| Calculated Tier List | Faixa calculada | Reuse `Faixas de dificuldade` family. |

### App / generic UI — high frequency

| English | Recommendation | Notes |
|---|---|---|
| Admin / Admin Settings | Admin / Configurações de admin | "Admin" untranslated as role name. |
| Add | Adicionar | |
| Add Player | Adicionar jogador | |
| All | Todos / Todas | Pick gender per key context. |
| Allow Repeats | Permitir repetições | |
| Anonymous | Anônimo | |
| Always | Sempre | |
| Avg Plate | Plate média | `Plate` stays English (established), modifier translated. |
| Avg Score | Pontuação média | |

## Process for future batches

1. Pick a feature folder (Tournaments, Tier Lists, Progress, Admin, Tools, etc.).
2. List its English keys (`grep -oP '(?<=L\[")[^"]+' ScoreTracker/ScoreTracker/Pages/<Folder>/**/*.razor` or similar).
3. Cross-reference against `App.pt-BR.resx` to find which are missing.
4. Translate using this glossary. **If a new term needs a decision, add a row to "Established term mappings" before translating.**
5. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.
6. PR titled like `Translate <Folder> to pt-BR (Phase N)`.
