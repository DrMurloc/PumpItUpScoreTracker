# es-MX localization glossary

Working reference for translating `App.en-US.resx` into `App.es-MX.resx`. Captures the conventions established by the original 84 hand-translated entries plus the 506-entry bulk batch on 2026-04-26 that brought the file to full coverage (596/596 keys).

For the localization mechanism itself (resx layout, `L["..."]` usage, key conventions), see [ARCHITECTURE.md](ARCHITECTURE.md#cross-cutting-concerns). For PIU domain terms in English, see [DOMAIN.md](DOMAIN.md). For the parallel ja-JP, ko-KR, pt-BR, and fr-FR conventions, see [LOCALIZATION-ja-JP.md](LOCALIZATION-ja-JP.md), [LOCALIZATION-ko-KR.md](LOCALIZATION-ko-KR.md), [LOCALIZATION-pt-BR.md](LOCALIZATION-pt-BR.md), and [LOCALIZATION-fr-FR.md](LOCALIZATION-fr-FR.md).

The bulk batch made structural decisions for the entries it added; those decisions are documented in the `2026-04-26 bulk batch — decisions and additions` section near the bottom of this file rather than retrofitted into the long tables (to keep the diff reviewable). A few of those decisions are deliberate divergences from the pre-existing file's conventions that future cleanup batches should propagate to the older entries — flagged below.

## Style conventions

- **Address the user with formal `usted`.** The existing prose voice leans `usted` (`Use el importador dev-tools si quiere reimportar`, `no salga de esta página`, `de lo contrario sus puntajes no serán guardadas`), with one mixed string that drops into informal `tú` mid-sentence (`tendrá que introducirlos siempre que uses esa opción` — see Known issues). Mexico web UIs use both `tú` and `usted`; this file already chose `usted`. Match that for new entries — `usted` / `su` / `sus`, imperative `Use`, `Salga`, `Importe`, etc. Don't introduce `tú` / `tu` / `tus` outside the existing inconsistencies.
- **Sentence case in values, even when the English key is Title Case.** Example: `"Difficulty Level"` → `"Nivel de dificultad"`, `"Phoenix Score Calculator"` → `"Calculadora de puntajes de Phoenix"`, `"Full Privacy Policy"` → `"Política de privacidad completa"`. The existing file mixes sentence case with English-influenced Title Case in several entries (`Conteo de Pasados`, `Tipo de Charts`, `Siguiente Letra`, `Subir Puntajes`, `Agregar a Favoritos`); standard Spanish orthography uses sentence case, so favor that for new entries and leave the older Title Case entries for a separate cleanup sweep — see Known issues.
- **Brand and proper-noun casing is preserved verbatim.** `Score Tracker`, `Phoenix`, `XX`, `PIU`, `PIUScores`, `Discord`, `https://piugame.com` — keep their original casing inside Spanish sentences.
- **Preserve positional placeholders verbatim.** `{0}`, `{1}`, `{2}` go into the value untouched, in whatever order Spanish grammar wants. Examples: `"Log In With"` (English value `"Log In With {0}"`) → `"Entrar con {0}"`; `"Vote Count"` → `"{0} Conteo de votos"`. (The latter is awkward — see Known issues.)
- **Skip prose with inline markup.** Per CLAUDE.md, `<MudText>` bodies with embedded `<MudLink>`/other elements stay hardcoded English. Don't extract them, don't translate them.
- **Use proper Spanish orthography.** Acute accents, tilde-ñ, opening question/exclamation marks — `áéíóúüñ¿¡`. Don't ASCII-fold. The existing file uses `¿…?` correctly (`¿Crear cuenta?`).
- **Mexican Spanish vocabulary preferences.** Use Mexico/LatAm forms over Peninsular Spanish where they diverge:
  - `puntaje` (not `puntuación`) — established (`Puntaje`, `puntajes`).
  - `computadora` (not `ordenador`) when the term comes up.
  - `descargar` for download (established, `Descargar puntajes`).
  - `subir` for upload (established, `Subir Puntajes`).
  - `borrar` for delete (established, `Borrar de la lista de Cosas que hacer`); `eliminar` is also acceptable.
  - `iniciar sesión` / `cerrar sesión` for login/logout (existing `Entrar sesión` / `Salir sesión` is awkward — see Known issues; new entries should use the standard forms when extending the login family).

## Established term mappings

These have at least one existing translation in `App.es-MX.resx`. New translations of the same term must reuse the established form unless there's a documented reason to change it.

### App / generic UI

| English | es-MX | Notes |
|---|---|---|
| Score Tracker (the app) | Score Tracker | Brand name, kept English. Used inside Spanish sentences as-is. |
| About | Acerca de | |
| Account | Cuenta | |
| Account Creation? | ¿Crear cuenta? | Opening `¿` per Spanish punctuation. |
| Actions | Acciones | |
| Add to Favorites | Agregar a Favoritos | Title Case on `Favoritos` — older entry. New "Add to X" should use sentence case (`Agregar a favoritos`). Mexico uses `Agregar` over Spain's `Añadir`. |
| Add to ToDo | Agregar a Cosas que hacer | "ToDo" rendered as `Cosas que hacer` (see To Do). |
| Average | Promedio | |
| Broken | Rota | Past participle, feminine default — likely modifying an implicit `chart` (treated as feminine here, see Charts gender note in Known issues). |
| Cancel | Cancelar | |
| Close | Cerrar | |
| Completed | Completado | Masculine default. |
| Difficulty Level | Nivel de dificultad | Sentence case. |
| Download Failures | Fallas en la descarga | |
| Download Scores | Descargar puntajes | |
| Easiest Player | Jugador más fácil | Comparative. Feminine form would be `Jugadora más fácil`. |
| Easy / Hard | Fácil / Difícil | Plain adjectives. |
| Favorites | Favoritos | |
| Full Privacy Policy | Política de privacidad completa | Sentence case. |
| Hardest Player | Jugador más difícil | |
| Hide Completed Charts | Esconder charts completos | `Esconder` rather than the more standard `Ocultar`; both are valid in Mexico — `Esconder` reads slightly more colloquial. Lean toward `Ocultar` for new "Hide" entries to match what other locales do consistently. |
| Language | (untranslated stub) | Existing entry is the English string `Language`. Translate as `Idioma` when next touched. |
| Log In With | Entrar con {0} | Verb form; placeholder takes the provider name (Discord, Google, Facebook). `Entrar` reads natural in Mexico, though `Iniciar sesión con {0}` is the more standard form — see Known issues for the login-family inconsistency. |
| Login | Entrar sesión | **Awkward** — should be `Iniciar sesión` (standard) or `Inicio de sesión` (noun). See Known issues. |
| Logout | Salir sesión | **Awkward** — should be `Cerrar sesión`. See Known issues. |
| Make Public / Private | Hacerla pública / Hacerla privada | Feminine `la` — implicit antecedent is `[la cuenta]`. Verb construction. |
| Medium | Medio | Masculine default. |
| Next Letter | Siguiente Letra | Title Case on `Letra` — older entry. Sentence case (`Siguiente letra`) for new entries. |
| Not Graded Count | No clasificado | `Count` left implicit. |
| Open Video | Abrir video | |
| Place (rank) | (untranslated stub) | Existing entry is the English string `Place`. Translate as `Posición` (matches pt-BR; more idiomatic than literal `Lugar` for a leaderboard column). |
| Public / Private | Pública / (no bare entry) | `Pública` (feminine) — implicit antecedent is `[la cuenta] pública`. Same fragility as fr-FR's `Publique` — as a bare on/off label without antecedent, the gender is fragile. For new bare-label entries, prefer the masculine `Público / Privado` unless context makes the feminine clear. |
| Report Video | Reportar video | |
| Report Video Tooltip | Reportar video de baja calidad, roto o incorrecto | |
| Restart | Reiniciar | |
| Save Scores | Guardar puntajes | |
| Submit | (untranslated stub) | Existing entry is the English string `Submit`. Translate as `Enviar` when next touched. |
| Text View | (untranslated stub) | Existing entry is the English string `Text View`. Translate as `Vista de texto`. |
| Title (in-game title award) | Título | "Title Progress" → `Progreso de títulos`; "Titles" → `Títulos`. |
| To Do | Cosas que hacer | Literal "things to do." Used compositionally: `Agregar a Cosas que hacer`, `Borrar de la lista de Cosas que hacer`. Alternative `Pendientes` (matches pt-BR) is more idiomatic and shorter; consider switching in a future cleanup. |
| Tools | Herramientas | |
| Total Count | Total | `Count` left implicit. |
| Tournaments | Torneos | |
| Upload Image | (untranslated stub) | Existing entry is the English string `Upload Image`. Translate as `Subir imagen` when next touched. |
| Upload Scores | Subir Puntajes | Title Case on `Puntajes` — older entry. Sentence case (`Subir puntajes`) for new uploads. |
| Upload XX Scores | Subir puntajes de XX | Sentence case here (inconsistent with Upload Scores above). |
| Used Primarily for debugging | Se usa principalmente para debugging | `debugging` left as English loanword. |
| Username | Usuario | |
| Very Easy / Very Hard | Muy fácil / Muy difícil | |
| Video | Video | Identical to English. (Spain uses `Vídeo` with accent; Mexico uses `Video`.) |
| Vote Count | {0} Conteo de votos | **Awkward placeholder + label compound.** Should be `{0} votos` or `Conteo de votos: {0}`. See Known issues. |
| 1+ Level Easier / Harder | 1+ Nivel más fácil / 1+ Nivel más difícil | Title Case on `Nivel` — older entries. Sentence case (`1+ nivel más fácil/difícil`) for new entries if possible. |

### PIU domain

| English | es-MX | Notes |
|---|---|---|
| Chart(s) | Charts | **Untranslated**, kept English (matches pt-BR `chart` policy). Used compositionally with Spanish articles: `Aleatorizador de charts`, `Tipo de Charts`, `Esconder charts completos`. Capitalization is inconsistent (`Charts` vs `charts` mid-sentence — see Known issues). |
| Chart Randomizer | Aleatorizador de charts | `Aleatorizador` is technically correct but unusual; pt-BR uses `Sorteador`. Either is acceptable. |
| Chart Type | Tipo de Charts | Title Case on `Charts`. Plural form here even though the English key is singular `Chart Type`. |
| CoOp | CoOp | Untranslated. |
| Singles / Doubles | Simples / Dobles | **Translated** — `Simples` for Singles, `Dobles` for Doubles. Distinct from pt-BR / fr-FR / ja-JP / ko-KR which all keep these English (or use loanwords). Mexican PIU community usage may vary; the choice is in force, but worth confirming with native players if a sweep happens. |
| Difficulty Level | Nivel de dificultad | Sentence case. |
| Letter Grade | Grado de letra | Translated. (Compare ja-JP `ランク`, ko-KR `랭크`, fr-FR `Rang (lettres)`, pt-BR `Letras de nota`.) |
| Mix | Versión | Translated, matches pt-BR `Versão`. (Compare ja-JP `バージョン`/`ベーション`, ko-KR `시리즈`, fr-FR `Mix` untranslated.) |
| Phoenix / XX | Phoenix / XX | Game versions, untranslated proper nouns. "Phoenix Score Calculator" → `Calculadora de puntajes de Phoenix`; "Upload XX Scores" → `Subir puntajes de XX`; "Import Phoenix Scores" → `Importar puntajes de Phoenix`. |
| Plate | Placa | **Translated** — same choice as fr-FR (`Plaque`). (Compare ja-JP `プレート` loanword, pt-BR `Plate` untranslated, ko-KR `플레이트` loanword.) |
| Pass / Passed / Not Passed | Pasados / No Pasados | "Passed Count" → `Conteo de Pasados`; "Not Passed Count" → `Conteo de No Pasados`. Title Case on the past participles — older entries. Sentence case (`Conteo de pasados` / `Conteo de no pasados`) for new entries. Note: this codebase translates `Pass` (unlike pt-BR / fr-FR which keep English `pass`). |
| Score (singular) | Puntaje | "Score" → `Puntaje`. Mexican/LatAm form (Spain uses `Puntuación`). |
| Scores (plural / collection) | Puntajes | "Save Scores" → `Guardar puntajes`; "Download Scores" → `Descargar puntajes`. |
| score / scores (lowercase, mid-sentence loanword) | scores | One legacy entry mixes English `scores` mid-sentence (`importe sus scores`). Don't propagate — translate to `puntajes` for new entries. See Known issues. |
| Phoenix Score Calculator | Calculadora de puntajes de Phoenix | Sentence case. |
| Saved Charts | Guardar Charts | **Wrong** — translates the past participle `Saved` as the infinitive verb `Guardar` ("to save"). Should be `Charts guardados`. See Known issues. |
| Song | (untranslated stub) | Existing entry is the English string `Song`. Translate as `Canción` (matches the Song-compound translations below). |
| Song Name | Nombre de canción | |
| Song Image | Imágen de canción | **Typo** — should be `Imagen de canción` (no accent on `i`). See Known issues. |
| Song Duration | Duración de canción | |
| Song Type | Tipo de canción | |
| Song Artist | (untranslated stub) | Existing entry is the English string `Song Artist`. Translate as `Artista de la canción` (matches the Song-compound family). |
| Tier Lists | (untranslated stub) | Existing entry is the English string `Tier Lists`. Likely keep untranslated as `Tier Lists` (matches fr-FR / pt-BR); alternatively translate as `Listas de niveles` if a translated form is preferred. |
| Leaderboard | Clasificación | Translated. (Compare ja-JP `ランキング`, ko-KR `리더보드`, fr-FR `Leaderboard` untranslated, pt-BR `Classificação`.) |
| Players | Jugadores | Masculine default plural. |
| Tournament(s) | Torneos | "Tournaments" → `Torneos`. Bare singular not yet present; use `Torneo` when needed. |
| Title (in-game title award) | Título | (Cross-reference: same as generic UI Title.) |
| Favorites | Favoritos | "Add to Favorites" → `Agregar a Favoritos`. |

### Game-mechanic vocabulary

| English | es-MX | Notes |
|---|---|---|
| Broken | Rota | Past participle, feminine — implicit antecedent treated as feminine (`[la chart] rota`). |
| debugging | debugging | Untranslated loanword. (`Se usa principalmente para debugging`.) |
| dev-tools | dev-tools | Untranslated loanword. (`Use el importador dev-tools`.) |

## Phrasing patterns to copy

- **Formal `usted` register.** Imperative forms like `Use`, `Salga`, `Importe`; possessives `su`, `sus`. Example: `Use el importador dev-tools si quiere reimportar puntajes más antiguos. Una vez iniciado, no salga de esta página, de lo contrario sus puntajes no serán guardadas.`
- **Possessives**: `sus puntajes`, `su cuenta`, `su contraseña`. Possessive precedes noun, agrees in number.
- **Spanish question/exclamation punctuation.** `¿…?` and `¡…!` with both opening and closing marks. Example: `¿Crear cuenta?`. Don't drop the opening marks.
- **English brand and PIU jargon stay verbatim mid-sentence.** `Score Tracker`, `Phoenix`, `XX`, `PIU`, `PIUScores`, `Discord`, `https://piugame.com`, `Charts`/`charts`, `CoOp`, `dev-tools`, `debugging`. Judgment terms (`Bad`, `Miss`, `Perfect`, `Great`, `Good`) and lifebar mechanics terminology have no es-MX precedent yet — recommend keeping them English when the keys arrive (matches the pt-BR / fr-FR PIU-jargon policy).
- **Decimal separator: comma.** Mexican Spanish uses `0,5` not `0.5` in formal prose, though `0.5` is increasingly common in technical contexts. Match whatever the source-language conventions imply.

## Known issues / native review needed

These were carried over from the existing translations and should be reviewed by a native speaker. Keep structural and quality changes separate diffs.

### Critical / awkward translations

- **`Login` → `Entrar sesión` / `Logout` → `Salir sesión`.** Both are non-idiomatic — Spanish requires a preposition (`Entrar a sesión` is also wrong because `entrar a` doesn't take `sesión`). Standard renderings:
  - `Login` (noun) → `Inicio de sesión` or `Iniciar sesión`.
  - `Logout` (verb / noun) → `Cerrar sesión`.
  - The verb-form `Log In With` should likewise be `Iniciar sesión con {0}` rather than `Entrar con {0}`.
  - **Fix as a single login-family sweep**, not piecemeal.
- **`Saved Charts` → `Guardar Charts`.** Wrong — translates the past participle `Saved` as the infinitive verb `Guardar` ("to save"). Should be `Charts guardados` (masculine plural agreeing with `Charts` as masculine — see gender note below).
- **`Vote Count` → `{0} Conteo de votos`.** The placeholder is unintegrated; reads as "{N} count of votes" rather than the intended "{N} votes" or "Vote count: {N}". Recommend `{0} votos` (matches the fr-FR `{0} votes` form) or `Conteo de votos: {0}`.

### Spelling typos

- **`Imágen` → `Imagen`.** In `Song Image` → `Imágen de canción`. The acute accent on `í` is wrong — `imagen` is a paroxytone ending in `n` preceded by a vowel, but it doesn't take a written accent in the singular. Plural `imágenes` does.
- **`puntajes más antiguas` → `puntajes más antiguos`.** In `Use Password 3`. Gender disagreement — `puntajes` is masculine, so the adjective must be `antiguos`, not `antiguas`.

### Register inconsistency

- **Mixed `usted` / `tú` in the same string.** `Use Password 2`: `No almacenamos un registro de tu usuario o contraseña, tendrá que introducirlos siempre que uses esa opción.` mixes informal `tu` (possessive) and `uses` (subjunctive) with formal `tendrá` (future). Pick one register and sweep:
  - **Recommended:** all `usted` — `No almacenamos un registro de su usuario o contraseña, tendrá que introducirlos siempre que use esa opción.`
  - Or all `tú` — `No almacenamos un registro de tu usuario o contraseña, tendrás que introducirlos siempre que uses esa opción.`
  - The other Use Password strings use `usted`, so the `usted` sweep is the lower-churn fix.
- **`importe sus scores`** (Use Password 1) — uses formal `Importe` and possessive `sus`, but mid-sentence English `scores` instead of `puntajes`. Convert to `puntajes` for consistency with the rest of the file.

### Capitalization inconsistencies

- **Mixed sentence case vs Title Case in values.** The file freely mixes sentence case (`Nivel de dificultad`, `Política de privacidad completa`, `Calculadora de puntajes de Phoenix`) with English-influenced Title Case (`Conteo de Pasados`, `Conteo de No Pasados`, `Tipo de Charts`, `Siguiente Letra`, `Subir Puntajes`, `Agregar a Favoritos`, `1+ Nivel más fácil`). Standard Spanish is **sentence case**. Recommend a one-shot sweep to lowercase non-initial, non-proper-noun words. Examples to fix:
  - `Conteo de Pasados` → `Conteo de pasados`
  - `Conteo de No Pasados` → `Conteo de no pasados`
  - `Tipo de Charts` → `Tipo de charts` (keep `Charts` capitalized only if treated as proper noun; otherwise lowercase mid-sentence)
  - `Siguiente Letra` → `Siguiente letra`
  - `Subir Puntajes` → `Subir puntajes` (matches the sibling `Subir puntajes de XX`)
  - `Agregar a Favoritos` → `Agregar a favoritos`
  - `1+ Nivel más fácil` → `1+ nivel más fácil`

### Charts capitalization and gender

The English loanword `Charts` is treated inconsistently:

- **Capitalized:** `Charts` (bare), `Tipo de Charts`, `Saved Charts → Guardar Charts`.
- **Lowercase:** `Aleatorizador de charts`, `Esconder charts completos`.

Gender: the past-participle adjectives in the file imply mixed gender:
- **Masculine plural** (implicit): `charts completos` ("hidden completed charts").
- **Feminine singular** (implicit): `Rota` for `Broken` (assumes `[la chart] rota`).
- **Should-be:** `Charts guardados` (masculine plural) for the wrongly-translated `Saved Charts`.

Pick one gender. Brazilian PIU community treats `chart` as **masculine** (per pt-BR glossary); recommend the same for es-MX since most of the existing implicit-agreement entries are already masculine (`completos`). Sweep `Rota` → `Roto` if `Broken` modifies a chart. Or if the implicit antecedent is `[la canción]` (feminine), the gender becomes feminine — disambiguate by context.

For lowercase vs capitalized: recommend **lowercase `charts`** mid-sentence (matches pt-BR), capitalized only when standalone.

### Questionable word choices

- **`Esconder` vs `Ocultar` for "Hide".** `Hide Completed Charts` → `Esconder charts completos`. `Esconder` reads more colloquial / "hide a physical object"; `Ocultar` is the more standard UI verb. Mexican usage accepts both, but `Ocultar` is cleaner for UI labels.
- **`Cosas que hacer` for "To Do".** Literal but verbose. `Pendientes` (matches pt-BR) is shorter and more idiomatic for a task-list label. Compounds would shrink: `Agregar a pendientes`, `Borrar de pendientes`. Consider switching in a future cleanup.
- **`Aleatorizador de charts` for "Chart Randomizer".** Technically correct but `Aleatorizador` is uncommon as a noun. Alternatives: `Sorteador de charts` (matches pt-BR `Sorteador de charts`), or `Generador aleatorio de charts`. Either reads more naturally.
- **`Grado de letra` for "Letter Grade".** Literal translation. Mexican PIU community usage isn't established; the term works but `Letra de calificación` or `Calificación` (just "grade") might fit better depending on context.
- **`Place` left untranslated as English.** Should be `Posición` (matches pt-BR `Posição`). `Lugar` is the literal translation but reads less naturally for a leaderboard column.

## Open decisions (terms upcoming batches will need)

Pulled from the ~516 untranslated keys. Each is a term that doesn't yet have an established es-MX translation in the resx, and that future batches will hit. Decide once per term, then add the row to **Established term mappings** above and use it.

### PIU domain — high frequency

| English | Recommendation | Notes |
|---|---|---|
| Pumbility | **Pumbility** (untranslated) | Proper noun for PIU's composite player rating; community uses the English term. Matches Phoenix/XX policy. |
| UCS | **UCS** (untranslated) | Acronym (User-Created Step). Untranslated everywhere else. |
| Bounty / Bounties | **Bounty / Bounties** (untranslated) | PIU community concept; matches pt-BR / ja-JP `バウンティ` / ko-KR `바운티` loanword treatment. Compounds: `Clasificación de Bounties` / `Tabla de Bounties`. |
| Stamina | **Stamina** (untranslated) | Matches pt-BR / fr-FR. |
| BPM | **BPM** | Untranslated. "Min BPM" → `BPM mín.`; "Max BPM" → `BPM máx.` (postfixed abbreviation matches pt-BR pattern). |
| Note Count | **Conteo de notas** | Matches the pt-BR `Contagem de notas` pattern. |
| Step Artist | **Autor de pasos** or **Step Artist** (untranslated) | pt-BR translates as `Autor dos passos`; fr-FR keeps English. Either works; lean toward translation for consistency with the Song-compound translations already established. |
| Combo | **Combo** | Untranslated loanword. |
| Lifebar | **lifebar** (lowercase loanword, untranslated) | Matches pt-BR. Bare `Life` → `Vida` when the standalone key arrives. |
| Folder (PIU difficulty group) | **folder** (lowercase loanword) | Matches pt-BR. |
| Judgment terms (Bad / Miss / Perfect / Great / Good) | **Bad / Miss / Perfect / Great / Good** (untranslated) | Matches pt-BR; PIU community uses English judgment terms. |
| Tag / Tags | **Tag / Tags** (untranslated loanwords) | Matches pt-BR. |

### App / generic UI — high frequency

| English | Recommendation | Notes |
|---|---|---|
| Add | Agregar | Already used (`Agregar a Favoritos`). Mexico prefers `Agregar` over Spain's `Añadir`. |
| All | Todos / Todas | Plural; gender per noun context. |
| Avatar | Avatar | Loanword. |
| Confirm | Confirmar | |
| Country | País | |
| Create | Crear | |
| Delete | Borrar | Already used (`Borrar de la lista de Cosas que hacer`); `Eliminar` is also acceptable. |
| Description | Descripción | |
| Done | Completado | Same as `Completed`. |
| Edit | Editar | |
| Filters | Filtros | |
| Hide | Ocultar | (Recommended over `Esconder` — see Known issues.) |
| Home | Inicio | |
| Image | Imagen | (No accent — see typo note.) |
| Last Updated | Última actualización | |
| Level | Nivel | |
| Min / Max (bare) | Mín. / Máx. | Abbreviated. Used as standalone column or filter labels. |
| Name | Nombre | |
| Open | Abierto | Or context-specific (e.g. `Abrir` verb). |
| Overview | Resumen general | |
| Page | Página | |
| Pending | Pendiente | |
| Reason | Razón / Motivo | |
| Save | Guardar | Already used. |
| Search | Buscar | |
| Settings | Configuración | |
| Show | Mostrar | |
| Show X | Mostrar X | Matches pt-BR `Mostrar X` pattern. |
| Welcome (greeting) | Bienvenido | Masculine default. |
| Welcome to Score Tracker, X! | ¡Bienvenido a Score Tracker, {0}! | Opening `¡` per Spanish punctuation. |

## 2026-04-26 bulk batch — decisions and additions

The 506-entry bulk batch on 2026-04-26 brought the es-MX file from 84 to 596 entries (full coverage of the en-US key set). It also fixed 10 untranslated stub entries that had been sitting with English-passthrough values (`Language` → `Idioma`, `Place` → `Posición`, `Submit` → `Enviar`, `Song` → `Canción`, `Song Artist` → `Artista de la canción`, `Upload Image` → `Subir imagen`, `Text View` → `Vista de texto`, plus the three `Make Public Disclaimer` / `Make Not Public Disclaimer` prose entries). `Charts`, `CoOp`, `Video`, and `Tier Lists` remain English-passthrough by design (loanwords).

The conventions below were committed by this batch. Future entries should reuse them.

### Conventions committed by the bulk batch

- **Sentence case throughout for new entries.** All ~500 new entries use sentence case (`Nivel competitivo`, `Distribución de puntajes`, `Calculadora de vida de PIU`, `Detalles del chart`). The older Title Case entries (`Conteo de Pasados`, `Tipo de Charts`, `Siguiente Letra`, `Subir Puntajes`, `Agregar a Favoritos`, `1+ Nivel más fácil`) are not retrofitted in this batch — fix in a separate sweep.
- **Formal `usted` register, sweepingly applied.** Every prose entry uses `usted` voice — `Su cuenta`, `sus puntajes`, `Use el botón`, `Asegúrese de`, `Tenga en cuenta`, `Vuelva a revisar`, `Si abandona esta página`. No `tú` / `tus` introduced. The pre-existing `Use Password 2` register-mix (`tu` + `tendrá` + `uses`) remains a known issue to sweep.
- **`Charts` lowercase mid-sentence.** New entries treat `charts` as a lowercase common-noun loanword in prose (`Lista de charts`, `Comparación de charts`, `Mostrar solo charts sugeridos`, `Tener {0} charts`). Capitalized `Charts` reserved for standalone labels and proper-noun-feeling positions (`Charts semanales`, `Top 50 {0} Charts` style). The older `Tipo de Charts` Title-Case form is left for the separate sweep.
- **`Charts` is masculine.** New agreement-bearing entries use masculine plural (`charts repetidos`, `charts sugeridos`, `charts completos`, `Chart guardado`, `Chart sugerido`, `Chart seleccionado`). The older feminine `Rota` (for `Broken`) remains a known issue — should become `Roto` if antecedent is `[el chart]`. Sweep alongside the Charts gender pass.
- **`Show` → `Mostrar` consistently.** All `Show X` entries use `Mostrar X` (matches pt-BR `Mostrar` and fr-FR `Montrer` patterns). No `Enseñar` / `Ver` variants introduced.
- **`Hide` → `Ocultar` consistently for new entries.** The older `Esconder charts completos` (using `Esconder`) remains as the lone pre-batch outlier; sweep when convenient. New `Hide X` entries are `Ocultar X` (`Ocultar chart para esta categoría`, `Ocultar charts sin record`, `Ocultar charts con cero de puntaje`).
- **PIU jargon stays English mid-sentence.** Confirmed for: `Pass`, `Pase`, `Step Artist`, `Spreadsheet`, `Stage Break`, `lifebar`, `Lifebar` (capitalized at start of label), `bad`, `miss`, `perfect`, `great`, `good`, `combo`, `run`, `play(s)`, `Rainbow Life`, `Stage Break`, `Stamina`, `Bounty`/`Bounties`, `Seed`, `folder`, `JSON`, `CSV`, `Spreadsheet`, `Phoenix`, `XX`, `PIU`, `PIUGame`, `dev tools`, `debugging`, `hacky`, `data-mine`, `JSON`, `TLDR`, `TRUE`/`FALSE`, judgment plurals (`bads`, `misses`, `perfects`, `greats`, `goods`). Lowercase when used as a generic noun in prose; capitalized when standalone label or proper-noun-feeling.
- **`Combo X / X Break` compound headers.** "Perfect Combo, Miss Break" → `Combo Perfect, Miss Break` (Spanish word order, English judgment + break terms verbatim).
- **`lifebar` lowercase loanword.** `lifebar` mid-prose (`la lifebar`, `su lifebar`, `desborde de la lifebar`); `Lifebar` capitalized only at start of a label (`Lifebar por nivel`, `Calculadora de lifebar`, `Descripción de la lifebar`, `Estadísticas de lifebar`).
- **`folder` lowercase loanword** for the PIU difficulty-level group (`folder objetivo`, `Promedios por folder`, `Distribución ponderada por folder`, `Puntajes promedio ponderados por folder`). Not capitalized, not translated as `Carpeta`.
- **`Tier List` is feminine.** `Tier List calculada`, `Tier List de PIU`. Feminine because of the implicit `lista`. Plural `Tier Lists` stays English (the existing entry remains a loanword passthrough).
- **`Plate` → `Placa(s)` continued.** New entries `Detalle de placas`, `Distribución de placas`, `Placas`, `Placa promedio` honor the existing `Placa` translation. (The pre-batch glossary flagged this as questionable vs. PIU community usage of `Plate` — decision deferred; `Placa` remains in force.)
- **`Pass` → `Pase` (singular noun) / `Pases` (plural) / `Pasados` (past participle for counts).** Reaffirmed and extended:
  - Bare `Pass` → `Pase`; `Stage Pass` → `Pase de etapa`; `Pass Rate` → `Tasa de pase`; `Pass (Data Backed)` → `Pase (con datos)`.
  - `Passed` (past participle) → `Pasado`; `Unpassed` → `No Pasado`.
  - `Passes` plural → `Pases`; compounds `Passes by Competitive Level` → `Pases por nivel competitivo`, `Passes By Level` → `Pases por nivel`.
  - The older `Conteo de Pasados` / `Conteo de No Pasados` (Title Case) remain — sweep to `Conteo de pasados` / `Conteo de no pasados` in the sentence-case batch.
- **`Tournament` → `Torneo` / `del torneo` family.** `Nombre del torneo`, `Configuración del torneo`, `Rol en el torneo`, `Fechas del torneo (EST)`. Compounds use `del torneo`, not `de torneo`.
- **`PUMBILITY` (uppercase) preserved.** When the source has `PUMBILITY` in caps, the value mirrors that. Lowercase `Pumbility` would be used elsewhere (none yet).
- **`Leaderboard` reserved for `Clasificación`.** New compound forms: `Clasificación mensual`, `Clasificación del chart`, `Clasificación de Bounties`, `Clasificaciones oficiales`, `Clasificaciones de completitud`, `Clasificaciones de UCS`, `Clasificación de qualifiers`, `Comparación de jugadores en clasificación`. Distinct from `Score Ranking(s)` / `World Rankings` → `Ranking(s)` (loanword, matches pt-BR).
- **`Ranking(s)` loanword for Score Rankings / World Rankings.** `Score Ranking` → `Ranking de puntaje`; `Score Rankings` → `Rankings de puntaje`; `World Rankings` → `Rankings mundiales`; `Scoring Rankings` → `Rankings de scoring`. The two-word distinction (`Clasificación` vs `Ranking`) is intentional — `Clasificación` is the Leaderboard family, `Ranking` is the comparative-score family. Same split as pt-BR.
- **`Qualifiers` → `qualifiers` (lowercase loanword).** Not `clasificatorias` (which would clash with `Clasificación` for Leaderboard). `Qualifiers Leaderboard` → `{0} clasificación de qualifiers`; `Qualifiers Submission` → `{0} envío de qualifiers`; `Sync Qualifier Leaderboard` → `Sincronizar clasificación de qualifiers`. Lowercase mid-sentence per the `pass`/`folder`/`run`/`play` loanword convention.
- **`Rating` untranslated.** Loanword (matches pt-BR / fr-FR). `Rating Calculator` → `Calculadora de Rating`; `Max Rating` → `Rating máx`; `ReCalculate Ratings` → `Recalcular Ratings`; `Your Difficulty Rating` → `Su Rating de dificultad`.
- **`Min/Max X` postfixed abbreviation.** `BPM mín`, `BPM máx`, `Nivel mín`, `Nivel máx`, `Vida máx`, `Conteo de notas mín`, `Conteo de notas máx`, `Grado de letra mín`, `Grado de letra máx`, `Rating máx`, `Puntaje mín`, `Puntaje máx`. Bare `Min` / `Max` as standalone labels → `Mín` / `Máx`. Full word `Minimum` / `Maximum` → `Mínimo` / `Máximo` (`Minimum Score` → `Puntaje mínimo`).
- **`Singles` / `Doubles` postfixed for level compounds.** `Singles Level` → `Nivel Singles`; `Doubles Level` → `Nivel Doubles`. Continues the existing `Simples` / `Dobles` translation for bare `Singles` / `Doubles` — but for `Singles vs Doubles` and the `Level` compounds, the loanword is preferred to match pt-BR/fr-FR conventions for the compound forms. (Bare `Singles` / `Doubles` keep the existing `Simples` / `Dobles` translation — known asymmetry, flag for native review.)
- **Decimal separator: comma.** `0,5`, `9,6` in prose. Half-width Arabic numerals throughout.
- **`Is X` boolean column headers drop the `Is` prefix.** `Is Warmup` → `Calentamiento` (matches pt-BR `Aquecimento` and fr-FR `Échauffement`). `IsBroken` and `XXLetterGrade` left untranslated as legacy property-name column headers (matches fr-FR pattern).
- **Page-name source preserved verbatim.** `ChartScoring`, `LetterDifficulties` (no spaces) kept as-is — these refer to specific page names.
- **Spanish punctuation throughout.** Opening `¿…?` and `¡…!` for all questions and exclamations: `¡Guardado!`, `¡Copiado al portapapeles!`, `¿Qué debería jugar?`, `¿No le gustaría saber?`, `¡Bienvenido a Score Tracker, {0}!`, `¡Agregado a la lista de Cosas que hacer!`. No bare `?` / `!` introduced.
- **`Shoutout` strings as `¡Un saludo a X por Y!`.** `Score Formula Shoutout` → `¡Un saludo a MR_WEQ por hacer ingeniería inversa de esta fórmula de puntaje!`; `Score Range Shoutout` → `¡Un saludo a daryen por recopilar datos y finalizar los rangos de puntaje para los grados de letra!`. Names stay verbatim (`MR_WEQ`, `daryen`, `KyleTT`, `FEFEMZ`, `Team Infinitesimal`, `DrMurloc`).
- **`Aviso:` prefix for disclaimers.** Replaces the English `Disclaimer:` prefix; matches the file's existing register (`Aviso: estos datos fueron extraídos por data-mine en NX2 y Prime; no está confirmado qué tan precisos son hoy.`, `Aviso: esta lista se está refinando; ...`). Use `Aviso:` (with half-width colon + space) at sentence start.
- **`Nota:` prefix for technical notes.** `Nota: la pérdida de puntaje puede tener un margen de 1-4 puntos por redondeo`, `Nota: esta herramienta es bastante hacky y puede requerir algo de debugging de su parte para que funcione.`. Same convention.
- **`TLDR:` prefix preserved.** Loanword acronym (matches fr-FR / pt-BR). `TLDR: los misses importan menos con poca vida...`.

### Established term mappings added 2026-04-26

These are now in effect. New entries in future batches should reuse them. (Recorded as a single block below rather than retro-fitted into the long tables above to keep the diff reviewable — same convention as fr-FR's bulk-batch section.)

#### PIU domain

| English | es-MX | Notes |
|---|---|---|
| Avg Plate | Placa promedio | Continues `Plate` → `Placa`. |
| Avg Score | Puntaje promedio | |
| Bounties | Bounties | Untranslated. |
| Bounty Leaderboard | Clasificación de Bounties | |
| BPM | BPM | Untranslated. |
| Calculated Tier List | Tier List calculada | Feminine. |
| Chart (singular) | Chart | Reaffirmed; gender now committed masculine. `Save Chart` → `Guardar chart`; `Chart saved` → `Chart guardado`. |
| Chart Average | Promedio del chart | |
| Chart Compare | Comparación de charts | |
| Chart Count / Chart Count By Level | Cantidad de charts / Cantidad de charts por nivel | |
| Chart Details | Detalles del chart | |
| Chart Difficulty by Letter Grade | Dificultad del chart por grado de letra | |
| Chart Leaderboard | Clasificación del chart | |
| Chart Score | Puntaje del chart | |
| Chart Statistics | Estadísticas del chart | |
| Chart Update | Actualización de chart | |
| Charts List | Lista de charts | |
| ChartScoring (page) | ChartScoring | Page-name no-space preserved. |
| Co-Op (with hyphen) | Co-Op | Distinct from `CoOp` (without hyphen) — keep both verbatim. |
| Combined | Combinado | |
| Community Completion | Completitud de la comunidad | |
| Competitive Level | Nivel competitivo | |
| Competitively | Competitivamente | |
| Completion | Completitud | |
| CoOp Aggregation | Agregación de CoOp | |
| Custom Scoring Formula | Fórmula de scoring personalizada | |
| Difficulty | Dificultad | |
| Difficulty By Letter / Difficulty By Player Level | Dificultad por letra / Dificultad por nivel de jugador | |
| Difficulty Categorization | Categorización por dificultad | |
| Difficulty Letters / Difficulty Passes / Difficulty Progress | Letras por dificultad / Pases por dificultad / Progreso por dificultad | |
| Difficulty Range | Rango de dificultad | |
| Doubles Level | Nivel Doubles | Postfixed `Doubles`. |
| Effective Level | Nivel efectivo | |
| Folder (PIU difficulty group) | folder | Lowercase loanword. Not `Carpeta`. |
| Folder Averages | Promedios por folder | |
| Folder Weighted Distribution | Distribución ponderada por folder | |
| Game Stats | Estadísticas del juego | |
| Lifebar / lifebar | lifebar | Lowercase loanword in prose; `Lifebar` capitalized only at start of a label. |
| Lifebar Calculator | Calculadora de lifebar | |
| Lifebar Description | Descripción de la lifebar | |
| Lifebar stats | Estadísticas de lifebar | |
| Letter Difficulty | Dificultad por letra | Same rendering as `Difficulty By Letter`. |
| LetterDifficulties (page) | LetterDifficulties | Page-name no-space preserved. |
| Life Bar by Level | Lifebar por nivel | |
| Life Threshold | Umbral de vida | |
| Max Life | Vida máx | |
| Max / Min (bare) | Máx / Mín | Postfixed abbreviation; `Min Score` → `Puntaje mín`. |
| Maximums / Minimums | Máximos / Mínimos | |
| Min Score / Avg Score / Max Score | Puntaje mín / Puntaje promedio / Puntaje máx | |
| Note Count | Conteo de notas | |
| Note Counts (plural) | Conteos de notas | |
| Official Leaderboards | Clasificaciones oficiales | |
| Pass | Pase | Singular noun. |
| Pass (Data Backed) | Pase (con datos) | |
| Pass Rate | Tasa de pase | |
| Passed / Unpassed | Pasado / No Pasado | |
| Passes (plural) | Pases | |
| Passes by Competitive Level / Passes By Level | Pases por nivel competitivo / Pases por nivel | |
| PIU Life Calculator | Calculadora de vida de PIU | |
| PIU Tier List | Tier List de PIU | |
| PIUGame Leaderboard Difficulty | Dificultad de la clasificación de PIUGame | |
| Plate Breakdown / Plate Distribution / Plates | Detalle de placas / Distribución de placas / Placas | Continues `Plate` → `Placa` mapping. |
| Play Count / X Plays | Cantidad de plays / `{0} plays` | Lowercase `plays` loanword. |
| PUMBILITY | PUMBILITY | Uppercase preserved. |
| Run (a play-through) | run | Lowercase loanword in prose. |
| Score (Data Backed) | Puntaje (con datos) | |
| Score Distribution | Distribución de puntajes | |
| Score Distribution Lines | Líneas de distribución de puntajes | |
| Score Distribution By Player Level | Distribución de puntajes por nivel de jugador | |
| Score Loss | Pérdida de puntaje por {0} | Translated rendering of the `{0} Score Loss` pattern; `por` introduces the cause (e.g. `Pérdida de puntaje por Greats`). |
| Score Ranking / Score Rankings | Ranking de puntaje / Rankings de puntaje | Loanword. Distinct from `Leaderboard` → `Clasificación`. |
| Score State | Estado del puntaje | |
| Scoring Difficulty | Dificultad de scoring | |
| Scoring Level | Nivel de scoring | |
| Scoring Level by Player Competitive Level | Nivel de scoring por nivel competitivo del jugador | |
| Scoring Rankings | Rankings de scoring | |
| Selected Chart | Chart seleccionado | Masculine. |
| Similar Players | Jugadores similares | |
| Singles Level | Nivel Singles | Postfixed `Singles`. |
| Singles vs Doubles | Singles vs Doubles | Identical. |
| Spreadsheet | Spreadsheet | Untranslated loanword. |
| Stage Break Modifier | Modificador de Stage Break | `Stage Break` kept English. |
| Stage Pass | Pase de etapa | |
| Stamina | Stamina | Untranslated loanword. |
| Stamina Session Builder | Constructor de sesión de stamina | |
| Starting Life | Vida inicial | |
| Step Artist (singular / plural) | Step Artist / Step Artists | Untranslated loanword. |
| Suggested Chart | Chart sugerido | Masculine. |
| Tier List (singular) | Tier List | Feminine. |
| Top 50 X | `Top 50 {0}` | |
| Tournament | Torneo | |
| Tournament Name / Tournament Settings / Tournament Role / Tournament Dates (EST) | Nombre del torneo / Configuración del torneo / Rol en el torneo / Fechas del torneo (EST) | `del torneo` compound. |
| UCS Leaderboard / UCS Leaderboards | Clasificación de UCS / Clasificaciones de UCS | |
| Ungraded | Sin grado | |
| Visible Life | Vida visible | |
| Weekly Charts | Charts semanales | |
| What Should I Play (?) | (¿)Qué debería jugar(?) | Both with-and-without-`?` source variants are present. |
| World Rankings | Rankings mundiales | |
| XX Progress | Progreso XX | No article (matches pt-BR pattern, sidesteps gender). |

#### Tournament / competition

| English | es-MX | Notes |
|---|---|---|
| Active / Upcoming / Previous Tournaments | Torneos activos / Torneos próximos / Torneos anteriores | |
| Always / Never (date fallbacks) | Siempre / Nunca | |
| Brackets (tournament) | Brackets | Untranslated loanword (PIU/tournament jargon). `{0} Brackets` → `Brackets de {0}` (reordered for Spanish structure). |
| End Date / Start Date | Fecha de finalización / Fecha de inicio | |
| In Person | En persona | |
| Location | Lugar | |
| Machines / Machine Name | Máquinas / Nombre de la máquina | |
| Player Name / New Player Name | Nombre del jugador / Nombre del nuevo jugador | |
| Players have X to play charts. … | Los jugadores disponen de {0} para jugar charts. … | |
| Qualifier Leaderboard (verb form `Sync …`) | Sincronizar clasificación de qualifiers | |
| Repeated charts X allowed. | Charts repetidos {0} permitidos. | `{0}` = `son` / `no son` (separately localized). |
| Seed | Seed | Untranslated. |
| Tournament Role | Rol en el torneo | |

#### App / generic UI

| English | es-MX | Notes |
|---|---|---|
| (Optional) Also delete historical data | (Opcional) Borrar también los datos históricos | |
| Additional Comments | Comentarios adicionales | |
| Admin | Admin | Loanword/role name. |
| Admin Settings | Configuración de admin | |
| All | Todos | Masculine plural default. |
| Allow Repeats | Permitir repeticiones | |
| Anonymous | Anónimo | |
| are / are not | son / no son | Used as substitution into `Repeated charts X allowed`. |
| Average Difficulty | Dificultad promedio | |
| Bad Suggestion / Good Suggestion | Mala sugerencia / Buena sugerencia | |
| Best Attempts / Best Score | Mejores intentos / Mejor puntaje | |
| Build Session | Construir sesión | |
| Bulk Vote | Voto en masa | |
| Category | Categoría | |
| Channel Name | Nombre del canal | |
| Clear Cache | Limpiar caché | |
| Combined | Combinado | |
| Community Invite | Invitación a la comunidad | |
| Competition | Competencia | |
| Confirm | Confirmar | |
| Content Lock | Bloqueo de contenido | |
| Copied to clipboard! | ¡Copiado al portapapeles! | |
| Could not find chart / song | No se pudo encontrar el chart / No se pudo encontrar la canción | |
| Couldn't parse JSON | No se pudo analizar el JSON | |
| Country | País | |
| Create | Crear | |
| Create Song | Crear canción | |
| Current | Actual | |
| Current Username / New Username | Nombre de usuario actual / Nuevo nombre de usuario | |
| Default | Predeterminado | |
| Delete / Delete All Scores | Borrar / Borrar todos los puntajes | |
| Description | Descripción | |
| Discord Id | Id de Discord | |
| Do It | Hacerlo | Admin trigger button. |
| Doesn't Match My Personal Skills | No coincide con mis habilidades personales | |
| Done | Completado | Same as `Completed`. |
| Download Example | Descargar ejemplo | |
| Duration | Duración | |
| Edit | Editar | |
| Estimated Point Gain Timeline | Cronología estimada de ganancia de puntos | |
| Example Set Builder | Constructor de set de ejemplo | |
| Existing | Existente | |
| Extra Settings | Configuración adicional | |
| File cannot be larger than 10 MB | El archivo no puede ser mayor a 10 MB | |
| Filters | Filtros | |
| Final Result / Final Result: X | Resultado final / `Resultado final: {0}` | |
| From / To (mapping) | De / A | |
| Hide | Ocultar | |
| Hide Chart for this Category | Ocultar chart para esta categoría | |
| Hide Record-less Charts / Hide Zero Scoring Charts | Ocultar charts sin record / Ocultar charts con cero de puntaje | |
| Home | Inicio | |
| I Don't Like The Chart | No me gusta el chart | |
| I Just Want to Hide The Chart | Solo quiero ocultar el chart | |
| Image Name | Nombre de la imagen | |
| Import Your Phoenix Scores | Importar sus puntajes de Phoenix | |
| Input Json | Entrada JSON | |
| Is Warmup | Calentamiento | `Is` prefix dropped per pt-BR / fr-FR pattern. |
| IsBroken / XXLetterGrade | IsBroken / XXLetterGrade | Untranslated — column headers matching legacy property names. |
| Korean Name | Nombre coreano | |
| Last Updated | Última actualización | |
| Letter Grade Template / TRUE/FALSE Template | Plantilla de grado de letra / Plantilla TRUE/FALSE | `Plantilla` prefix. |
| Levels | Niveles | |
| Level/Players | Nivel/Jugadores | Combined input label. |
| Link | Enlace | |
| Location | Lugar | |
| Lock Status / Locked / Unlocked | Estado del bloqueo / Bloqueado / Desbloqueado | |
| Lock User / Unlock User | Bloquear usuario / Desbloquear usuario | |
| Machine Name | Nombre de la máquina | |
| Median | Mediana | |
| Minimum Score | Puntaje mínimo | |
| Minutes / Seconds | Minutos / Segundos | |
| Missing | Faltante | |
| Monthly Leaderboard | Clasificación mensual | |
| Monthly Total | Total mensual | |
| My Relative Difficulty | Mi dificultad relativa | |
| New Player Name | Nombre del nuevo jugador | |
| No Recorded Scores | Sin puntajes registrados | |
| None | Ninguno | Masculine default. |
| Not Relevant to Category | No relevante para la categoría | |
| Note Count: X | `Conteo de notas: {0}` | |
| Notes | Notas | |
| Original Concept (excel score tracking) Constructed by KyleTT | Concepto original (seguimiento de puntajes en Excel) construido por KyleTT | |
| Other | Otro | |
| Overall Letters / Overall Passes | Letras generales / Pases generales | |
| Overview | Resumen general | |
| Parsed Scores | Puntajes analizados | |
| Percentile Distribution | Distribución por percentil | |
| Permissions | Permisos | |
| Player | Jugador | |
| Player added | Jugador agregado | |
| Player Levels | Niveles de jugadores | |
| Player To Test (Must Be Set To Public) | Jugador a probar (debe estar configurado como Pública) | |
| Player Weights | Pesos de jugadores | |
| Players (Paste UserId from Account Page if not Public) | Jugadores (pegue el UserId de la página de cuenta si no es Pública) | |
| Players synced | Jugadores sincronizados | |
| Points / Points Per Second / Points Pre-Score | Puntos / Puntos por segundo / Puntos antes del puntaje | |
| Potential Conflict | Conflicto potencial | |
| PreBuilt Tournament Configuration | Configuración de torneo prearmada | |
| Privacy Policy | Política de privacidad | Sentence case (diverges from older `Política de privacidad completa` which already used sentence case — consistent). |
| Priority | Prioridad | |
| Private User - X | `Usuario privado - {0}` | |
| Reason | Razón | |
| Record Session | Registrar sesión | |
| Removed | Borrado | |
| Removed from ToDo List! / Added to ToDo List! | ¡Borrado de la lista de Cosas que hacer! / ¡Agregado a la lista de Cosas que hacer! | |
| Rest Time / Rest Time Per Chart: X | Tiempo de descanso / `Tiempo de descanso por chart: {0}` | |
| Restored | Restaurado | |
| Save | Guardar | Already used. |
| Save Chart | Guardar chart | |
| Saved! | ¡Guardado! | |
| Score: X / Session Score | `Puntaje: {0}` / Puntaje de sesión | |
| Search User (Name or UserId) | Buscar usuario (nombre o UserId) | |
| Seconds of Rest Per Chart | Segundos de descanso por chart | |
| See Leaderboards | Ver clasificaciones | |
| Set Charts | Definir charts | |
| Settings | Configuración | |
| Show | Mostrar | |
| Show Extra Info | Mostrar información adicional | |
| Show Only Suggested Charts | Mostrar solo charts sugeridos | |
| Show Scoreless | Mostrar sin puntaje | |
| Show Top Only | Mostrar solo el top | |
| Site constructed and maintained by DrMurloc | Sitio construido y mantenido por DrMurloc | |
| Source / Source Code | Fuente / Código fuente | |
| Standard Low / Standard High | Bajo estándar / Alto estándar | |
| Start | Iniciar | Verb sense. |
| Stats | Estadísticas | |
| Step Artist: X | `Step Artist: {0}` | |
| Supported Formats | Formatos compatibles | |
| Target Player Level | Nivel del jugador objetivo | |
| Test Scores / Test With Player Data | Puntajes de prueba / Probar con datos del jugador | |
| The Category Isn't Interesting to Me | La categoría no me interesa | Source apostrophe-typo not preserved. |
| Title | Título | |
| TLDR | TLDR | Acronym preserved. |
| Total / Total Charts: X | Total / `Total de charts: {0}` | |
| Total Chart Bonus | Bono total del chart | |
| Total Popularity Singles vs Doubles / Total Singles vs Doubles | Popularidad total Singles vs Doubles / Total Singles vs Doubles | |
| Type | Tipo | |
| Unknown | Desconocido | |
| Updated X Y | `{0} {1} actualizado` | Snackbar after a chart save. |
| Uploader | Uploader | Untranslated loanword (matches the `dev tools` / `debugging` / `hacky` loanword treatment in the existing prose). |
| Use Script | Usar script | Source has a stray double-space (`Use  Script`); collapsed in the value. |
| User locked / User unlocked | Usuario bloqueado / Usuario desbloqueado | |
| Verification | Verificación | |
| Video URL | URL del video | |
| Video info is not formatted correctly | La información del video no tiene el formato correcto | |
| Welcome / Welcome to Score Tracker, X! | Bienvenido / `¡Bienvenido a Score Tracker, {0}!` | |
| Week X - Top Y Charts | `Semana {0} - Top {1} charts` | |
| Wouldn't You Like To Know | ¿No le gustaría saber? | Easter-egg tooltip; uses `usted` despite playful tone. |
| X Brackets | `Brackets de {0}` | |
| X Charts | `{0} charts` | |
| X Note counts | `Conteos de notas {0}` | `{0}` = chart type. |
| X Plays | `{0} plays` | Lowercase `plays`. |
| X Progress | `Progreso {0}` | |
| X% of Y Comparable Players | `{0}% de {1} jugadores comparables` | |
| Your account is content-locked. … | Su cuenta está bloqueada en cuanto a contenido. Envíe un mensaje a un admin si tiene preguntas. | |
| Your Difficulty Rating | Su Rating de dificultad | |
| Your Score / Your Points / Your Points per Second | Su puntaje / Sus puntos / Sus puntos por segundo | |
| Youtube Hash | Hash de YouTube | |

#### Multi-line prose (Life Calculator, ChartScoring, etc.)

The Life Calculator and ChartScoring pages have ~30 dense paragraph entries. They were translated mechanically from the glossary; native-speaker review priority is **high** for these — same caveat as the ja-JP, ko-KR, and fr-FR equivalents. Specific candidates:

- All `Life loss description` / `Life gain description` / `Recovery Observations` paragraphs.
- The `Highlighted players have a recorded score on the chart in question.` / `These averages are shifted away from the level in question by .5 of a standard deviation …` algorithm-explainer paragraphs on /Experiments/ChartScoring.
- The `For CoOps, scoring level is simply the lowest level player who's been able to pass the chart.` paragraph and its neighbors on /Experiments/ChartScoring.
- The `When at 12% or lower visual life, a miss gives less life loss than a bad…` and `Misses or back-to-back Bads early in a run…` paragraphs on /PIULifeCalculator.

Short labels (column headers, button text) are likely fine.

## Process for future batches

1. Pick a feature folder (Tournaments, Tier Lists, Progress, Admin, Tools, etc.) or a category from the Known issues list above.
2. List its English keys (`grep -oP '(?<=L\[")[^"]+' ScoreTracker/ScoreTracker/Pages/<Folder>/**/*.razor` or similar).
3. Cross-reference against `App.es-MX.resx` to find which are missing.
4. Translate using this glossary. **If a new term needs a decision, add a row to "Established term mappings" before translating.**
5. For inconsistency fixes (login family, sentence-case sweep on the older Title-Case entries, `Imágen` → `Imagen`, `Esconder` → `Ocultar` for the one outlier, `Cosas que hacer` → `Pendientes`, `Rota` → `Roto` for the Charts gender sweep, register normalization in `Use Password 2`, the `Simples`/`Dobles` vs `Singles`/`Doubles` split-treatment), do **one batch per category** so the diff is reviewable.
6. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.
7. PR titled like `Translate <Folder> to es-MX` or `Fix es-MX <inconsistency>`.
