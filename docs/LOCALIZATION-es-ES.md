# es-ES localization glossary

Working reference for translating `App.en-US.resx` into `App.es-ES.resx`. Bootstrapped 2026-07-10 from the volunteer intake form filled by **Errlena** (Discord PiuSpain) — the 2026-07 full-coverage batch (808/808 keys) was generated from that form. The 11 example translations from the intake §4 shipped **word-for-word** and anchor the style below.

es-ES is deliberately distinct from [es-MX](LOCALIZATION-es-MX.md): different register (tú/vosotros vs usted), different vocabulary (media/fallo/añadir vs promedio/falla/agregar), and a much heavier English-loanword lean (score, leaderboard, plate, rating all stay English here but are translated in es-MX). Do not copy strings between the two Spanish files.

For the localization mechanism itself (resx layout, `L["..."]` usage, key conventions), see [ARCHITECTURE.md](ARCHITECTURE.md). For PIU domain terms in English, see [DOMAIN.md](DOMAIN.md).

## Style conventions

- **Address the player with informal `tú`**; plural is `vosotros` (both per intake — it's what the community's Discord uses). Possessives `tu`/`tus`, imperatives `Juega`, `Asegúrate`, `vuelve a intentarlo`, `no salgas`. Never `usted`/`su`. The one shipped vosotros form: `Espero que fuerais de la mano en Canon D`.
- **Buttons use the infinitive** (`Guardar Scores`, `Ocultar`, `Crear sesión`) — intake §3.2, anchored by `Guardar Scores`.
- **Inclusive gender with a slash** where Spanish forces a choice: `Bienvenido/a`, `Jugador/a` (intake §3.3). Generic plurals stay masculine (`Jugadores`).
- **Question marks are paired, exclamation marks are not.** The volunteer wrote `¿eres tú?` but `Guardado!` and `Bienvenido/a a Score Tracker, {0}!` — so questions get `¿…?` and exclamations get closing `!` only. This mirrors casual es-ES typing. **Flagged for native/owner review** — see below; a sweep to add `¡` is trivial if the community prefers formal orthography.
- **Sentence case for Spanish words; English loanwords capitalized in short button/label positions, lowercase mid-prose.** Anchors: `Grabar sesión` (sentence case), `Guardar Scores` / `Ocultar Charts Conseguidos` (loanwords capitalized in buttons), `ningún score que ya se haya guardado` (lowercase in prose).
- **Spain vocabulary, not LatAm** — the intake's pitfall warning was Latin-American forms creeping in. In force: `media` (not `promedio`), `fallo` (not `falla`), `añadir` (not `agregar`), `vídeo` (accented), `ordenador` if it ever comes up, `competición` (not `competencia`), `ajustes` (not `configuración`), `hoja de cálculo` for Spreadsheet.
- **Decimal separator: comma** in prose (`9,6`, `0,5 desviaciones estándar`).
- **Judgment names are English and capitalized, pluralized Spanish-style**: `los Misses`, `los Bads`, `los Perfects`, `los Greats`, `los Goods` (anchored by intake §4.10).
- **Brand and proper nouns verbatim**: `Score Tracker`, `PIUScores`, `Phoenix`, `XX`, `PIU`, `Discord`, `Start.GG`, `World` (community name), `PUMBILITY` (caps preserved), song/artist names never translated.
- **`Aviso:` prefix for disclaimers/notes** (`Disclaimer:`/`Note:` in source) — avoids colliding with `nota` = letter grade.
- **Skip prose with inline markup.** `<MudText>` bodies with embedded elements stay hardcoded English — same rule as every locale.

## Established term mappings

Decisions from the intake §2 (volunteer's call or accepted AI draft). New translations must reuse these.

### PIU domain

| English | es-ES | Source |
|---|---|---|
| chart | `chart` (EN; masculine, `el chart`, lowercase mid-prose) | intake draft accepted |
| level | `nivel` | intake draft accepted |
| Singles / Doubles | `Singles` / `Doubles` (EN) | intake draft accepted |
| CoOp | `CoOp` (EN) | intake draft accepted |
| pass (verb) | `pasar` | intake draft accepted |
| pass (noun) | `pass` (EN; masculine, plural `passes`) | intake draft accepted |
| break / broken | `break` / `broken` (EN) | **volunteer's call** |
| play / run (one attempt) | `intento` (`Número de intentos`, `{0} intentos`) | **volunteer's call** — her intake open question, answered via Discord 2026-07-12 (replaced the batch's initial `run` guess) |
| session | `sesión` | intake draft accepted; `Grabar sesión` anchored |
| step artist | `step artist` (EN) | intake draft accepted |
| score | `score` (EN; masculine, `el score`, `ningún score`) | **volunteer's call** (rejects both `puntuación` and es-MX `puntaje`) |
| Perfect / Great / Good / Bad / Miss | EN, capitalized, Spanish plural article | intake draft accepted |
| letter grade | `nota` | intake draft accepted |
| plate | `plate` (EN; masculine) | intake draft accepted |
| lifebar / life | `barra de vida` / `vida` | intake draft accepted |
| rating | `rating` (EN; masculine) | intake draft accepted |
| Pumbility | `Pumbility` / `PUMBILITY` (EN) | intake draft accepted |
| Score Tracker | `Score Tracker` (EN) | intake draft accepted |
| song / artist | `canción` / `artista` | intake draft accepted |
| Mix | `versión` | intake draft accepted |
| folder | `folder` (EN; masculine) | intake draft accepted |
| skill | `skill` (EN; feminine, `las skills`) | **volunteer's call** |
| tier list | `tier list` (EN; feminine); menu title stays `Tier Lists` | intake draft accepted (§4.1 left blank → EN) |
| title (award) | `título` | intake draft accepted |
| bounty | `bounty` (EN) | intake draft accepted |
| weekly challenge | `reto semanal`; `Weekly Charts` → `Charts semanales` | intake draft accepted |
| leaderboard | `leaderboard` (EN; masculine, plural `leaderboards`) | **volunteer's call** (rejects `clasificación`) |
| ranking(s) | `ranking(s)` (EN) | intake draft accepted |
| tournament | `torneo` | intake draft accepted |
| qualifiers | `clasificatorias` | intake draft accepted (note: translated even though leaderboard isn't — no clash since `clasificación` is unused) |
| bracket | `bracket` (EN) | intake draft accepted |
| seed | `seed` (EN) | intake draft accepted |
| stamina | `stamina` (EN) | intake draft accepted |
| to perfect (verb) | `hacer un PG` | **volunteer's call** |
| Rainbow (lifebar state) | `Rainbow` (EN; `vida Rainbow`, `barra de vida Rainbow`) | intake draft accepted |
| completed (charts) | `conseguido(s)` | anchored by §4.5 `Ocultar Charts Conseguidos` |

### Batch decisions (translator's choice — see native review)

Made during the 2026-07 full-coverage batch where the intake was silent. Reuse them; challenge them only via the native-review list.

| English | es-ES | Notes |
|---|---|---|
| Average / Avg | `media` / `medio` postfix (`Score medio`, `Plate medio`, `Dificultad media`) | Spain form; never `promedio` |
| Failure(s) | `fallo(s)` (`Fallos de descarga`, `Fallos de lectura`) | Spain form; never `falla` |
| Add | `añadir` | Spain form; never `agregar` |
| Delete / Remove | `eliminar` / `quitar` (from lists) | |
| Settings | `ajustes` | es-ES app convention |
| Login / Logout | `Iniciar sesión` / `Cerrar sesión`; `Log In With {0}` → `Iniciar sesión con {0}` | |
| Upload / Download | `subir`/`subida` / `descargar` | `subida` anchored by §4.11 |
| To Do | `pendientes` (`Añadir a pendientes`, `Mostrar solo charts pendientes`) | |
| Hide / Show | `ocultar` / `mostrar` | |
| Completion (feature) | `completado` (`Leaderboards de completado`, `Completado de la comunidad`) | es-ES gamer usage (`% de completado`) |
| Note Count | `recuento de notas` | `notas` here = arrows, not grades — context disambiguates |
| Play Count / X Plays | `número de intentos` / `{0} intentos` | attempt = `intento` per volunteer follow-up 2026-07-12 |
| Passed / Unpassed | `pasado(s)` / `sin pasar` | participle translated; noun stays `pass` |
| Pass Rate | `tasa de passes` | |
| Min / Max | `mín.` / `máx.` postfix (`BPM mín.`, `Nivel máx.`); full words `mínimo`/`máximo` where the source spells them out | |
| Letter-grade plurals (As/Ss/SSs/SSSs) | verbatim | |
| Spreadsheet | `hoja de cálculo` | one of the few loanwords translated — Excel-adjacent office vocab, not PIU jargon |
| Recap (season recap feature) | `Recap` (EN; masculine, `el Recap`, `Mi Recap`, `Phoenix Recap`) | |
| Upscore / Backfill / Paragon / Stage Pass / Break | EN verbatim | community/technical jargon |
| Chart Randomizer | `Randomizador de charts` | loanword lean |
| Video | `vídeo` (accented) | Spain spelling |
| Report (a video) | `reportar` | widespread in es-ES gaming despite LatAm origin — flag if it grates |
| Recorded (scores) | `registrado` (`Fecha de registro`, `Sin scores registrados`) | `grabar` reserved for sessions per §4.2 |
| GameTag / UserId / JSON / CSV / API / BPM / PPS / TLDR / dev tools / debugging / hacky | verbatim | |
| XXLetterGrade / IsBroken / ChartScoring / LetterDifficulties | verbatim | legacy property names / page names, same as every locale |

Added by the 2026-07-12 main-merge batch (tier-lists overhaul, PIU Center crawler, legacy mixes, theme):

| English | es-ES | Notes |
|---|---|---|
| Ranked by / Grouped by | `Ordenado por` / `Agrupado por` | tier-list lens selectors |
| Tiers | `Tiers` (EN) | tier-list jargon, matches `tier list` |
| Pass/Score Difficulty (lenses) | `Dificultad de pass` / `Dificultad de score` | |
| Community Rating (lens) | `Rating de la comunidad` | continues `rating` EN |
| Better Than (lens) | `Mejor que` | percentile bands verbatim (`0 -> 10%` etc., decimal comma `99,9%`) |
| Personalized (tier list) | `Personalizada` | feminine, agrees with `tier list` |
| Comfortable / Compact / Table (density) | `Cómoda` / `Compacta` / `Tabla` | feminine, implicit `vista` (Gmail-style) |
| Display | `Visualización` | |
| tier bands (rating) | `Increíble` / `Muy bueno` / `Bueno` / `Bajo` / `Muy bajo` | **`Good` key = `Bueno`** — band label, not the judgment (judgments only appear as plural keys) |
| age bands | `Ancestral` / `Muy antiguo` / `Antiguo` / `Nuevo` / `Muy nuevo` / `Más reciente` | |
| popularity bands | `Ultra popular` … `Ultra impopular` | |
| cleared | `superado(s)` | distinct from `pasado` (passed) |
| crawl / snapshot / sustain | EN verbatim (`crawl de PIU Center`, `snapshot`, `Tiempo de sustain`) | admin/technical jargon |
| PIU Center | verbatim | external site name; `aesthete` credited verbatim |
| mix names (Prime, Fiesta, NX, Exceed / Zero, Premiere / Prex, Classic, American) | verbatim | intake: version names stay as-is |
| Why Don't You Get Up and Dance, Man? | verbatim | song title, never translated |
| Score History | `Historial de scores` | |
| 1st (rank badge) | `1.º` | |

## Native review needed

Ordered by importance. These shipped as translator's choice or verbatim-anchor extrapolations; a native speaker (ideally Errlena) should confirm.

1. **Opening `¡` omitted on all exclamations** (`Guardado!`, `Copiado al portapapeles!`, `Bienvenido/a a Score Tracker, {0}!`). Extrapolated from the intake §4 anchors, which used `¿…?` but never `¡`. If that was an oversight rather than a style choice, sweep the file to add `¡…!` pairs. Questions keep `¿…?` either way. (Her known intake open question turned out to be play/run, since resolved — this one still needs her explicit confirmation.)
2. **`TLDR:` prefix dropped** in the life-calculator summary — the volunteer's §4.10 translation omitted it (shipped verbatim). The standalone `TLDR` header key kept it. Confirm whether the prefix should return.
3. **Loanword capitalization in buttons** (`Subir Scores`, `Ocultar Charts Conseguidos`) vs sentence case (`Grabar sesión`) — the §4 anchors are internally inconsistent; the batch capitalized loanwords in button labels and lowercased them in prose. Confirm the pattern.
4. **`Lamp de folder`** (`Folder Lamp`) — "lamp" is imported rhythm-game jargon; no es-ES precedent known. Alternatives: `Folder completo`, `Lámpara`.
5. **`Completado` for the Completion feature** — chosen over `compleción` (stiff) and keeping EN. |
6. **`conseguido` generalized** from the §4.5 anchor to the standalone `Completed` key — if `Completed` ever labels non-chart things, revisit.
7. **`clasificatorias`** (accepted draft) coexists with EN `leaderboard`/`brackets`/`seed` — confirm the tournament scene actually says it.
8. **Archetype/recap flavor names** (`Cazador/a de passes`, `Copo de nieve especial`, `Alma social`, `Momentos matagigantes`, `Espero que fuerais de la mano en Canon D`) — playful translations of site-invented labels; wordsmithing welcome.
9. **`Reportar vídeo`** — `reportar` is LatAm-rooted but common in es-ES gaming; alternative `Denunciar`/`Informar de`.
10. **Multi-line prose pages** (Life Calculator, ChartScoring explainer, import flows) — translated mechanically from the glossary; same high-priority review caveat as every other locale's bulk batch.

## Intake form provenance

- Volunteer: **Errlena** (Discord PiuSpain), es-ES confirmed, 2026-07.
- §3.4 (overall loanword lean) was left blank; the batch inferred a strong loanword lean from her §2 answers (9 of the ~30 terms explicitly forced to EN, zero forced to Spanish).
- §4.1 (`Tier Lists`) was the only example left blank → shipped EN per her §2 tier-list row.
- Her §4 pitfall note: machine translation failures in es-ES are about **expressions and whole sentences**, not word choice — prioritize sentence-level naturalness over term-table fidelity when the two conflict.
- The filled form arrived as a markdown export; any Word/Google-Docs comments did not survive conversion. Her one flagged open question — the play/run row — was resolved directly over Discord on 2026-07-12: **`intento`**.

## Process for future batches

1. New keys land in `App.en-US.resx` → translate into `App.es-ES.resx` in the same PR (same rule as all locales).
2. Use the term mappings above; **if a new term needs a decision, add a row here before translating.**
3. Keep the tú/vosotros register, the no-`¡` convention (until reviewed), and the Spain-vocabulary list.
4. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` confirms resx well-formedness.
5. Native-review sweeps (e.g. the `¡` question) should be one batch per category so the diff is reviewable.
