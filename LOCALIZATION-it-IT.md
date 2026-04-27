# it-IT localization glossary

Working reference for translating `App.en-US.resx` into `App.it-IT.resx`. Unlike the other locale glossaries — which captured conventions inferred from existing translations — this one is **prescriptive**: there are no Italian entries in the resx yet, so this file establishes the conventions ahead of the first translation batch. Decisions here become "established" the moment they're applied to the resx.

For the localization mechanism itself (resx layout, `L["..."]` usage, key conventions), see [ARCHITECTURE.md](ARCHITECTURE.md#cross-cutting-concerns). For PIU domain terms in English, see [DOMAIN.md](DOMAIN.md). For the parallel ja-JP, ko-KR, pt-BR, fr-FR, and es-MX conventions, see [LOCALIZATION-ja-JP.md](LOCALIZATION-ja-JP.md), [LOCALIZATION-ko-KR.md](LOCALIZATION-ko-KR.md), [LOCALIZATION-pt-BR.md](LOCALIZATION-pt-BR.md), [LOCALIZATION-fr-FR.md](LOCALIZATION-fr-FR.md), and [LOCALIZATION-es-MX.md](LOCALIZATION-es-MX.md).

> **Status (2026-04-26):** Glossary only. No `App.it-IT.resx` exists yet, and `it-IT` is not yet listed in the supported-cultures array in [Program.cs](ScoreTracker/ScoreTracker/Program.cs). Both will be wired up when the first translation batch lands.

## Style conventions

- **Address the user with informal `tu`.** Italian consumer-app and game-UI convention is overwhelmingly `tu` / `tuo` / `tuoi` / `tua` / `tue`, not the formal `Lei` / `Suo`. PIU is a game / community tool, not a banking or government service — informal voice fits the audience. This matches pt-BR (`você`) and ja-JP (polite-`です／ます`-not-keigo) in tone, and diverges from fr-FR (`vous`) and es-MX (`usted`). Imperatives use the `tu` form: `Aggiungi`, `Salva`, `Carica`, `Mostra`, `Nascondi`, `Verifica`, `Assicurati`. Possessives `tuo` / `tuoi` / `tua` / `tue`. Subject pronoun usually omitted (`Sei al primo posto!` not `Tu sei al primo posto!`) per Italian norms.
- **Sentence case in values, even when the English key is Title Case.** Italian orthography uses sentence case — only the first word and proper nouns are capitalized. Examples: `"Difficulty Level"` → `"Livello di difficoltà"`, `"Phoenix Score Calculator"` → `"Calcolatore di punteggi Phoenix"`, `"Full Privacy Policy"` → `"Informativa sulla privacy completa"`. Don't carry English Title Case into Italian values. The only exceptions are brand and PIU-jargon proper nouns (see next bullet).
- **Brand and proper-noun casing is preserved verbatim.** `Score Tracker`, `Phoenix`, `XX`, `PIU`, `PIUScores`, `PIUGame`, `Discord`, `Start.GG`, `https://piugame.com` — keep their original casing inside Italian sentences.
- **Preserve positional placeholders verbatim.** `{0}`, `{1}`, `{2}` go into the value untouched, in whatever order Italian grammar wants. Examples: `"Log In With"` → `"Accedi con {0}"`; `"Welcome to Score Tracker, X!"` → `"Benvenuto su Score Tracker, {0}!"`; `"Recorded On X"` → `"Registrato il {0}"`.
- **Skip prose with inline markup.** Per CLAUDE.md, `<MudText>` bodies with embedded `<MudLink>`/other elements stay hardcoded English. Don't extract them, don't translate them. Splitting prose into fragment keys produces poor translations across languages with different word order.
- **Use proper Italian orthography.** Grave and acute accents on stressed final vowels — `àèéìòù`. The most frequent in this UI will be `à` (`difficoltà`, `città`, `attività`, `qualità`), `è` (the verb "is" — distinct from the conjunction `e` "and"), and `ò`/`ù` (`può`, `più`). Apostrophes on elisions (`l'utente`, `un'immagine`, `dell'account`, `nell'app`). Don't ASCII-fold — write `città`, not `citta'` or `citta`.
- **Italian punctuation matches English ASCII.** Standard `?`, `!`, `:`, `;`, `,`, `.` with regular spaces. No NBSP-before-punctuation rule (unlike French) and no inverted opening marks (unlike Spanish). Quotation marks: prefer ASCII `"..."` for consistency with the English source; native Italian guillemets `«...»` are correct but not worth the churn here.
- **Decimal separator: comma.** Italian uses `0,5` not `0.5` in prose. Half-width Arabic numerals throughout (matches fr-FR / es-MX / pt-BR conventions).
- **Apostrophe contractions.** Italian elides the article before vowel-initial nouns: `l'utente`, `l'immagine`, `un'opzione` (feminine `un'`), `un account` (masculine, no apostrophe — be careful with the gender rule), `dell'utente`, `nell'app`, `all'account`. Always use a straight ASCII apostrophe `'`, never a curly `'`.

## Recommended term mappings

These are the prescriptive defaults for the first translation batch. Once an entry is in `App.it-IT.resx`, this section becomes "Established term mappings" and future batches must reuse the form. **If a term needs a different rendering in a particular context, document the divergence here before translating.**

### App / generic UI

| English | it-IT | Notes |
|---|---|---|
| Score Tracker (the app) | Score Tracker | Brand name, kept English. Used inside Italian sentences as-is. |
| About | Informazioni | "About The Site" → `Informazioni sul sito`. |
| Account | Account | Loanword, masculine (`l'account`, `un account`, `il mio account`). |
| Account Creation? | Crea un account? | Phrased as a prompt rather than a literal noun phrase. |
| Actions | Azioni | |
| Add | Aggiungi | Imperative `tu` form. |
| Add to Favorites | Aggiungi ai preferiti | |
| Add to ToDo | Aggiungi alla ToDo List | `ToDo List` kept English mid-sentence (matches fr-FR pattern); article `alla` (feminine, agreeing with implicit `lista`). |
| Age | Età | Feminine. "Show Age" → `Mostra età`. |
| All | Tutti / Tutte | Masculine plural default `Tutti`; use `Tutte` when the modified noun is feminine plural (`Tutte le canzoni`). |
| Anonymous | Anonimo | Masculine default. |
| Average | Media | Feminine. |
| Avatar | Avatar | Loanword, masculine (`l'avatar`). |
| Broken | Rotto | Masculine default; agrees with `[il chart]` per the gender decision below. |
| Cancel | Annulla | Imperative. |
| Category | Categoria | Feminine. |
| Channel Name | Nome del canale | |
| Clear Cache | Svuota cache | |
| Close | Chiudi | |
| Combined | Combinato | Masculine default. |
| Communities | Comunità | Italian invariable feminine — singular and plural identical (`la comunità` / `le comunità`). |
| Completed | Completato | Masculine default; agrees with the implicit antecedent. |
| Confirm | Conferma | |
| Copied to clipboard! | Copiato negli appunti! | `appunti` is the standard Italian for clipboard; masculine plural. |
| Copy Script | Copia lo script | `script` masculine loanword. |
| Copy to Clipboard | Copia negli appunti | |
| Country | Paese | Masculine. |
| Create | Crea | |
| Current | Attuale | |
| Default | Predefinito | Masculine default. |
| Delete | Elimina | (`Cancella` is also acceptable; `Elimina` is the more standard UI verb in Italian.) |
| Description | Descrizione | Feminine. |
| Difficulty | Difficoltà | Feminine, invariable plural (`la difficoltà` / `le difficoltà`). Note the grave-accent `à`. |
| Difficulty Level | Livello di difficoltà | |
| Done | Fatto | Same as `Completed` semantically but differently phrased per English source. Use `Fatto` for `Done`, `Completato` for `Completed`. |
| Download | Scarica | Verb, imperative. "Download Scores" → `Scarica i punteggi`. |
| Download Failures | Errori di download | `download` loanword retained as the noun. |
| Download Scores | Scarica i punteggi | |
| Duration | Durata | Feminine. |
| Easiest Player | Giocatore più facile | Comparative. Feminine form would be `Giocatrice più facile`; masculine default fits the English source's gender-neutral intent. |
| Easy / Hard | Facile / Difficile | Plain adjectives, both end in `-e` and are gender-invariable in the singular. |
| Edit | Modifica | |
| Ending Page | Pagina finale | |
| Event | Evento | Masculine. |
| Existing | Esistente | |
| Extra Settings | Impostazioni extra | |
| Favorites | Preferiti | Masculine plural. |
| Filters | Filtri | Masculine plural. |
| From / To (mapping) | Da / A | |
| Full Privacy Policy | Informativa sulla privacy completa | "Privacy policy" is `informativa sulla privacy` in standard Italian legal/web prose; `privacy` itself is the loanword. |
| Hide | Nascondi | Imperative. |
| Hide Completed Charts | Nascondi i chart completati | Article `i` for masculine plural per Charts gender decision below. |
| Home | Home | Loanword for the sidebar nav label, kept English (Italian web UI convention). Alternative `Pagina iniziale` works too; pick `Home` for brevity. |
| Image | Immagine | Feminine (`l'immagine`, `le immagini`). |
| Image Name | Nome dell'immagine | |
| Language | Lingua | Feminine. |
| Last Updated | Ultimo aggiornamento | |
| Level | Livello | Masculine. |
| Levels | Livelli | |
| Link | Link | Loanword, masculine (`il link`). Plural also `link` (loanwords are invariable in Italian). Alternative `collegamento` is correct but more formal/dated for a UI label. |
| Location | Luogo | |
| Lock Status / Locked / Unlocked | Stato del blocco / Bloccato / Sbloccato | |
| Login (noun) | Login | Loanword, masculine. Distinct from "Log In With" verb form below. |
| Log In With | Accedi con {0} | Verb form. Placeholder takes the provider name (Discord, Google, Facebook). |
| Logout | Esci | Verb form ("exit/log out"). Alternative noun `Logout` (loanword) is fine for column headers. |
| Make Public / Private | Rendi pubblico / Rendi privato | Verb construction. Masculine agreement with implicit `[il profilo]` / `[l'account]`. |
| Max / Min (bare) | Max / Min | Loanword abbreviations as standalone column or filter labels. |
| Maximums / Minimums | Massimi / Minimi | Used when the English source spells them out. |
| Maximum / Minimum (full word) | Massimo / Minimo | Used when English source spells it out (`Minimum Score` → `Punteggio minimo`). |
| Median | Mediana | Feminine. |
| Medium | Medio | Masculine default. |
| Min/Max BPM | BPM min / BPM max | Postfixed lowercase abbreviation (matches the BPM/postfix patterns in fr-FR / es-MX). |
| Min/Max Letter Grade | Voto in lettere min / Voto in lettere max | See `Letter Grade` in PIU domain. |
| Min/Max Note Count | Conteggio note min / Conteggio note max | |
| Minutes / Seconds | Minuti / Secondi | |
| Missing | Mancante | |
| My Score | Il mio punteggio | Article `Il` because Italian possessives normally take the article. |
| Name | Nome | Masculine. |
| Next Letter | Prossima lettera | Feminine agreement with `lettera`. |
| None | Nessuno | Masculine default. |
| Not Graded Count | Non valutati | "Count" left implicit. Plural masculine. |
| Notes | Note | Feminine plural. |
| Open | Apri | Verb sense. |
| Other | Altro | Masculine default. |
| Overview | Panoramica | Feminine. |
| Page | Pagina | Feminine. |
| Pending | In attesa | |
| Permissions | Autorizzazioni | Feminine plural. |
| Photos | Foto | Italian invariable plural. |
| Place (rank) | Posizione | "1st place" sense. (Matches pt-BR `Posição`, es-MX `Posición`.) |
| Player | Giocatore | Masculine. Feminine `Giocatrice` for an explicitly-female player; gender-neutral default is masculine. |
| Players | Giocatori | Masculine plural default. |
| Player Count | Numero di giocatori | |
| Popularity | Popolarità | Feminine, invariable plural. |
| Priority | Priorità | Feminine, invariable plural. |
| Privacy Policy | Informativa sulla privacy | |
| Private | Privato | Masculine default. |
| Progress | Progresso | Masculine. |
| Public | Pubblico | Masculine default. As a bare on/off label without antecedent, masculine is the safer choice (compare fr-FR `Publique` and es-MX `Pública` issues — Italian avoids the trap with the masculine default). |
| Reason | Motivo | |
| Recorded Date | Data di registrazione | |
| Recorded On X | Registrato il {0} | Article `il` before the date placeholder. |
| Remove from ToDo | Rimuovi dalla ToDo List | Mirrors `Aggiungi alla ToDo List`. |
| Removed | Rimosso | Masculine default. |
| Report Video | Segnala video | |
| Restart | Riavvia | |
| Restored | Ripristinato | |
| Rules | Regole | Feminine plural. |
| Save | Salva | |
| Save Scores | Salva i punteggi | |
| Saved! | Salvato! | Snackbar. Masculine default. |
| Score | Punteggio | Singular noun. (Italian doesn't have a `puntaje`/`pontuação` problem — `punteggio` is universal.) |
| Scores | Punteggi | Plural. |
| Score (Data Backed) | Punteggio (con dati) | Mirrors es-MX `Puntaje (con datos)` and fr-FR `Score (Avec Données)`. |
| Score Loss | Perdita di punteggio per {0} | `per` introduces the cause (e.g. `Perdita di punteggio per Greats`). Avoids the gender-agreement pitfall fr-FR hit with `liée aux`. |
| Score State | Stato del punteggio | |
| Scores Parsed | Punteggi analizzati | |
| Search | Cerca | |
| Search User | Cerca utente | "Search User (Name or UserId)" → `Cerca utente (nome o UserId)`. |
| Settings | Impostazioni | Feminine plural. |
| Show | Mostra | |
| Show X | Mostra X | Suffix pattern: `Show Skills` → `Mostra le abilità`, `Show Difficulty` → `Mostra la difficoltà`, `Show Song Name` → `Mostra il nome della canzone`, `Show Step Artist` → `Mostra lo Step Artist`, `Show Age` → `Mostra l'età`. Article (`i`/`il`/`la`/`le`/`lo`/`l'`) per gender and initial-letter rules of the modified noun. (Italian `lo` before `s+consonant`, `z`, `gn`, `ps`, `x`, `y` — hence `lo Step Artist`.) |
| Site | Sito | Masculine. |
| Skill | Abilità | Feminine, invariable plural. |
| Source | Fonte | Feminine. |
| Source Code | Codice sorgente | |
| Standard Low / Standard High | Standard basso / Standard alto | |
| Start | Inizia | Verb sense. |
| Stats | Stats | Loanword acceptable; alternative `Statistiche`. Lean `Statistiche` for column headers, `Stats` for short labels. |
| Statistics | Statistiche | Feminine plural. |
| Submit | Invia | Standard Italian for form submit. |
| Submission Page | Pagina di invio | |
| Suggested Chart | Chart suggerito | Masculine agreement per Charts gender decision below. |
| Tag / Tags | Tag / Tag | Loanword, masculine. Italian loanwords are invariable in plural — don't write `Tags`. |
| Text View | Vista testo | Or `Visualizzazione testo`; pick the shorter for a column header. |
| Title | Titolo | "Title Progress" → `Progresso dei titoli`; "Titles" → `Titoli`. |
| TLDR | TLDR | Acronym preserved (matches all other locales). |
| To Do | Da fare | Bare "To Do" → `Da fare`. Compounds keep `ToDo` English (`ToDo List`, `ToDo Charts`). |
| Tools | Strumenti | Masculine plural. |
| Total | Totale | |
| Total Count | Totale | `Count` left implicit. |
| Tournaments | Tornei | |
| Type | Tipo | Masculine. |
| Unknown | Sconosciuto | Masculine default. |
| Upload | Carica | Verb form. (`Carica` is the natural Italian for "upload"; `Caricamento` is the noun.) Loanword `Upload` acceptable as a noun column header but `Carica` is preferred for the verb. |
| Upload Image | Carica immagine | |
| Upload Scores | Carica i punteggi | (Avoid the `Télécharger` direction-confusion bug fr-FR shipped — `Carica` = upload, `Scarica` = download. Don't swap.) |
| Upload XX Scores | Carica i punteggi XX | |
| Uploader | Uploader | Loanword, masculine. (Italian `caricatore` is more common for "battery charger" than for "person who uploads.") |
| Use Script | Usa script | |
| Used Primarily for debugging | Usato principalmente per il debugging | `debugging` loanword. |
| Username | Nome utente | |
| Validation | Validazione | Feminine. (Or `Verifica` if context is the act of verifying.) |
| Verification | Verifica | |
| Very Easy / Very Hard | Molto facile / Molto difficile | |
| Video | Video | Identical, masculine, invariable. |
| Video URL | URL del video | |
| Vote Count | {0} voti | Lowercase `voti` after placeholder (matches fr-FR `{0} votes`; avoids the awkward `Conteggio voti: {0}` form). |
| Welcome | Benvenuto | Masculine default. |
| Welcome to Score Tracker, X! | Benvenuto su Score Tracker, {0}! | |
| Website | Sito web | |
| Wouldn't You Like To Know | Non vorresti saperlo? | Easter-egg tooltip. Stays informal `tu`. |
| 1+ Level Easier / Harder | 1+ livello più facile / 1+ livello più difficile | Sentence case (lowercase `livello` mid-label). |

### PIU domain

| English | it-IT | Notes |
|---|---|---|
| Chart(s) | Chart / Chart | **Untranslated**, kept English (matches pt-BR / fr-FR / es-MX). Italian plural is **invariable** — don't write `Charts`. So `il chart` (singular masculine) and `i chart` (plural masculine). The English source's `Charts` becomes Italian `chart` mid-sentence. **Chart is masculine** in Italian (matches es-MX `Charts` masculine; same reasoning — the implicit antecedent is `[il diagramma]` / `[il livello]` / `[lo schema]`, all masculine). New entries that bear adjectival agreement use masculine: `chart completati`, `chart suggerito`, `chart sugeriti`, `chart salvato`. |
| Chart List | Lista dei chart | |
| Chart Randomizer | Generatore casuale di chart | Or loanword `Randomizer di chart`. Pick the translated form for accessibility; loanword acceptable. |
| Chart Type | Tipo di chart | Sentence case. |
| CoOp | CoOp | Untranslated proper-noun-feeling acronym. |
| Co-Op (with hyphen) | Co-Op | Distinct source variant; preserve verbatim. |
| Singles / Doubles | Singles / Doubles | **Untranslated** (matches fr-FR / pt-BR / ja-JP — the PIU community uses English). Italian `Singolo` / `Doppio` would be correct natural translations but the community-jargon convention wins. (es-MX is the outlier with `Simples`/`Dobles`.) |
| Difficulty Level | Livello di difficoltà | |
| Letter Grade | Voto in lettere | Translated. (Compare ja-JP `ランク`, ko-KR `랭크`, fr-FR `Rang (lettres)`, es-MX `Grado de letra`, pt-BR `Letras de nota`.) `Voto` alone could mean a numeric grade; the parenthetical-ish `in lettere` disambiguates. Min/Max forms keep `in lettere`: `Voto in lettere min/max`. |
| Mix | Mix | Untranslated proper-noun-feeling term. (Compare es-MX/pt-BR `Versión`/`Versão`, ja-JP `バージョン`, ko-KR `시리즈`, fr-FR `Mix`.) Italian `Versione` is correct but the PIU community uses `Mix` verbatim. |
| Phoenix / XX | Phoenix / XX | Game versions, untranslated proper nouns. "Phoenix Score Calculator" → `Calcolatore di punteggi Phoenix` (Phoenix postfixed). "Import Phoenix Scores" → `Importa i punteggi Phoenix`; "Upload XX Scores" → `Carica i punteggi XX`. |
| Plate | Plate | **Untranslated**, kept English (matches pt-BR). Italian translation `Placca` reads as "plaque" / "metal plate" and is an over-translation of the in-game term. fr-FR (`Plaque`) and es-MX (`Placa`) translated; their glossaries flag this as a questionable choice. Italian skips the trap and keeps `Plate`. The in-game tier abbreviations (`MG`, `PG`, `UG`) stay untranslated regardless. |
| Pass / Passed / Not Passed | Pass / Passed / Non Passed | **Untranslated**, kept English (matches fr-FR). The PIU community uses `pass` as a verb mid-sentence: `che non hai passato in Pass`. Capitalized `Pass` for standalone label, lowercase `pass` for mid-sentence verb-noun usage. (es-MX/pt-BR translate to `Pase`/`Pase` — Italian leans English-loanword.) |
| Stage Pass / Stage Break | Stage Pass / Stage Break | Untranslated PIU jargon. |
| Score (singular) | Punteggio | (Cross-reference: App / generic UI table.) |
| Scores (plural) | Punteggi | |
| Score State | Stato del punteggio | The state values (`Passed`, `Unpassed`, `Unscored`) stay English. |
| Phoenix Score Calculator | Calcolatore di punteggi Phoenix | Sentence case. |
| Note Count | Conteggio note | (Or `Numero di note` — `Conteggio note` is more compact for column headers.) |
| Note Counts (plural) | Conteggi note | |
| BPM | BPM | Untranslated. Min/Max forms postfix lowercase: `BPM min`, `BPM max`. |
| Step Artist | Step Artist | **Untranslated** loanword (matches fr-FR; pt-BR translates as `Autor dos passos`). Italian PIU community uses the English term. Italian translation `Autore dei passi` is grammatically fine but rarely used in practice. Plural `Step Artist` (loanwords are invariable). |
| Tier Lists | Tier Lists | Untranslated loanword (matches fr-FR / es-MX). Italian `liste di livelli` is correct but unidiomatic for PIU community. |
| Tier List (singular) | Tier List | **Feminine** (implicit `[la lista]`). `Tier List calcolata`, `Tier List PIU`. |
| Calculated Tier List | Tier List calcolata | |
| Leaderboard | Classifica | **Translated**. Italian `Classifica` (feminine) is the natural and idiomatic UI word. Matches pt-BR `Classificação` and es-MX `Clasificação` family. (Diverges from fr-FR `Leaderboard` untranslated and ja-JP `ランキング` loanword.) Compounds: `Classifica mensile`, `Classifica del chart`, `Classifica dei Bounty`, `Classifiche ufficiali`, `Classifiche di completamento`, `Classifiche UCS`. |
| Score Ranking / Score Rankings | Classifica per punteggio / Classifiche per punteggio | Same `Classifica` family. (es-MX splits Leaderboard `Clasificación` from Ranking `Ranking` loanword; Italian uses `Classifica` for both — there's no Italian-internal reason to introduce a `Ranking` loanword.) |
| World Rankings | Classifiche mondiali | |
| Scoring Rankings | Classifiche di scoring | `scoring` loanword. |
| Communities | Comunità | (Cross-reference.) |
| Country | Paese | (Cross-reference.) |
| UCS | UCS | Untranslated acronym. "Add UCS" → `Aggiungi UCS`; "UCS Leaderboard" → `Classifica UCS`. Treated as feminine when an article is required (`la UCS` — implicit `[la coreografia utente]`); often used without an article. |
| Players | Giocatori | (Cross-reference.) |
| Player Count | Numero di giocatori | |
| Tournaments | Tornei | |
| Tournament | Torneo | Compounds: `Nome del torneo`, `Impostazioni del torneo`, `Ruolo nel torneo`, `Date del torneo` — `del/nel torneo` per the preposition's case. |
| Qualifiers | Qualifiers | **Untranslated** (lowercase mid-sentence loanword). Italian `qualificazioni` is correct but reads heavily; PIU tournament community uses the English term. `Qualifiers Leaderboard` → `Classifica dei Qualifiers`. |
| Rating | Rating | **Untranslated** loanword (matches fr-FR / pt-BR / es-MX). Italian `valutazione` exists but the community uses `Rating`. "Max Rating" → `Rating max`; "Rating Calculator" → `Calcolatore di Rating`. |
| Pumbility | Pumbility | Untranslated proper-noun-feeling term (PIU's composite player rating). Capitalized when referenced as a label. The all-caps `PUMBILITY` source variant is preserved as `PUMBILITY`. |
| Song | Canzone | Feminine (`la canzone`, `le canzoni`). |
| Song Name | Nome della canzone | |
| Song Image | Immagine della canzone | |
| Song Duration | Durata della canzone | |
| Song Type | Tipo di canzone | |
| Song Artist | Artista della canzone | (Italian: `artista` is invariable in gender — `l'artista` works for both male and female artists.) |
| Personalized Difficulty | Difficoltà personalizzata | |
| Scoring Level | Livello di scoring | `scoring` loanword. |
| Scoring Difficulty | Difficoltà di scoring | |
| Bounty / Bounties | Bounty / Bounty | Untranslated, invariable plural per Italian loanword rules. `Bounty Leaderboard` → `Classifica dei Bounty`. |
| Stamina | Stamina | Italian word identical to English in this sense — fortunate cognate. Untranslated. |
| Lifebar / lifebar | lifebar | Lowercase loanword in prose; `Lifebar` capitalized only at start of a label (matches all other locales). |
| Lifebar Calculator | Calcolatore di lifebar | |
| Lifebar Description | Descrizione della lifebar | |
| Lifebar stats | Statistiche della lifebar | |
| Folder (PIU difficulty group) | folder | Lowercase loanword. **Not** `cartella` (which is "folder" in the file-system sense — would confuse). Matches fr-FR / es-MX / pt-BR. |
| Folder Averages | Medie per folder | |
| Folder Weighted Distribution | Distribuzione ponderata per folder | |
| Combo | Combo | Loanword, masculine, invariable. |
| Run (a play-through) | run | Lowercase loanword in prose. |
| Play / Plays | play / plays | Lowercase loanword, invariable. `Play Count` → `Numero di plays`; `X Plays` → `{0} plays`. |
| Spreadsheet | Spreadsheet | Untranslated loanword (matches all other locales). Italian `foglio di calcolo` exists but reads heavily. |
| Seed (tournament) | Seed | Untranslated. |
| Brackets (tournament) | Bracket | Untranslated, invariable plural. `{0} Brackets` → `Bracket di {0}` (reordered for Italian structure). |
| Weekly Charts | Chart settimanali | Lowercase `chart` mid-label. |
| Community Completion | Completamento della comunità | |
| Completion | Completamento | |
| Competitive Level | Livello competitivo | |
| Effective Level | Livello effettivo | |
| Difficulty Categorization | Categorizzazione per difficoltà | |
| Difficulty Range | Intervallo di difficoltà | |
| Average Difficulty | Difficoltà media | |
| Min/Avg/Max Score | Punteggio min / Punteggio medio / Punteggio max | Postfixed lowercase `min`/`max`; full word `medio`. |
| Min/Max Level | Livello min / Livello max | Postfixed lowercase. |
| Score Distribution | Distribuzione dei punteggi | |
| Score Distribution Lines | Linee di distribuzione dei punteggi | |
| Score Distribution By Player Level | Distribuzione dei punteggi per livello del giocatore | |
| Plate Breakdown / Plate Distribution / Plates / Avg Plate | Dettaglio Plate / Distribuzione Plate / Plate / Plate medio | `Plate` invariable (untranslated, see PIU domain table above). |
| Pass Rate | Tasso di Pass | `Pass` capitalized as the proper-noun-feeling label. |
| Passes (plural) | Pass | Italian invariable plural for the loanword. |
| Passes by Competitive Level | Pass per livello competitivo | |
| Passes By Level | Pass per livello | |
| Singles Level | Livello Singles | Postfixed loanword. |
| Doubles Level | Livello Doubles | Postfixed loanword. |
| Singles vs Doubles | Singles vs Doubles | Identical. |
| Stage Break Modifier | Modificatore Stage Break | `Stage Break` kept English. |
| Starting Life | Vita iniziale | |
| Max Life | Vita max | |
| Visible Life | Vita visibile | |
| Life Threshold | Soglia di vita | |
| Life Bar by Level | Lifebar per livello | |
| PIU Life Calculator | Calcolatore di vita PIU | |
| Stamina Session Builder | Generatore di sessione Stamina | |
| Build Session | Crea sessione | |
| Record Session | Registra sessione | |
| Session Score | Punteggio della sessione | |
| Rest Time | Tempo di riposo | |
| Rest Time Per Chart: X | Tempo di riposo per chart: {0} | |
| Seconds of Rest Per Chart | Secondi di riposo per chart | |
| Selected Chart | Chart selezionato | Masculine. |
| Suggested Chart | Chart suggerito | Masculine. |
| Set Charts | Imposta i chart | |
| See Leaderboards | Vedi le classifiche | |
| Top 50 X | Top 50 {0} | |
| What Should I Play (?) | Cosa dovrei giocare(?) | Both with-and-without-`?` source variants present. |
| XX Progress | Progresso XX | No article (matches pt-BR/fr-FR/es-MX pattern, sidesteps gender ambiguity). |
| Title (in-game title award) | Titolo | "Title Progress" → `Progresso dei titoli`; "Titles" → `Titoli`. |
| Favorites | Preferiti | (Cross-reference.) |

### Game-mechanic vocabulary

| English | it-IT | Notes |
|---|---|---|
| Broken | Rotto | Past participle, masculine default — agrees with implicit `[il chart]`. |
| debugging | debugging | Untranslated loanword. |
| dev-tools | dev-tools | Untranslated loanword. |
| JSON / CSV | JSON / CSV | Untranslated. |
| Bad / Miss / Perfect / Great / Good (judgment terms) | Bad / Miss / Perfect / Great / Good | **Untranslated** — PIU community uses English judgment terms verbatim. Plurals: `Bads`, `Misses`, `Perfects`, `Greats`, `Goods` (English plural; do NOT Italianize to invariable `Bad`/`Miss`/etc — these are quoted-feeling jargon, not common-noun loanwords). Lowercase mid-prose, capitalized when standalone label. Matches all other locales. |

### Tournament / competition

| English | it-IT | Notes |
|---|---|---|
| Active / Upcoming / Previous Tournaments | Tornei attivi / Tornei prossimi / Tornei precedenti | |
| Always / Never (date fallbacks) | Sempre / Mai | |
| End Date / Start Date | Data di fine / Data di inizio | |
| In Person | Di persona | |
| Location | Luogo | |
| Machines / Machine Name | Macchine / Nome della macchina | |
| Player Name / New Player Name | Nome del giocatore / Nome del nuovo giocatore | |
| Players have X to play charts. … | I giocatori hanno {0} per giocare i chart. … | |
| Qualifier Leaderboard (verb form `Sync …`) | Sincronizza la classifica dei Qualifiers | |
| Repeated charts X allowed. | I chart ripetuti {0} consentiti. | `{0}` = `sono` / `non sono` (separately localized). |
| Tournament Role | Ruolo nel torneo | |

### Admin / settings

| English | it-IT | Notes |
|---|---|---|
| Admin | Admin | Loanword/role name. |
| Admin Settings | Impostazioni admin | |
| Bulk Vote | Voto in massa | |
| Discord Id | Id Discord | |
| Do It | Fallo | Admin trigger button. Imperative + clitic `lo`. |
| Letter Grade Template / TRUE/FALSE Template | Modello voto in lettere / Modello TRUE/FALSE | `Modello` prefix; `TRUE`/`FALSE` left English (matches fr-FR / es-MX). |
| Lock User / Unlock User | Blocca utente / Sblocca utente | |
| Permissions | Autorizzazioni | (Cross-reference.) |
| Player added | Giocatore aggiunto | |
| Players synced | Giocatori sincronizzati | |
| User locked / User unlocked | Utente bloccato / Utente sbloccato | |

## Phrasing patterns to copy

- **Informal `tu` register.** Subject pronoun usually omitted. Imperatives `Aggiungi`, `Salva`, `Carica`, `Mostra`, `Nascondi`, `Modifica`, `Conferma`, `Annulla`. Possessives `tuo` / `tuoi` / `tua` / `tue` precede their noun. Example: `Se non hai un account con Score Tracker, ne verrà creato uno quando selezionerai uno dei metodi di autenticazione.`
- **`Nota:` prefix for technical notes.** `Nota: la perdita di punteggio può avere un margine di 1-4 punti per arrotondamento.`, `Nota: questo strumento è sperimentale.`. Half-width colon + space at sentence start.
- **`Avviso:` prefix for disclaimers.** Replaces the English `Disclaimer:` prefix. `Avviso: questi dati sono stati estratti da NX2 e Prime; non è confermato quanto siano accurati oggi.` Half-width colon + space at sentence start.
- **`TLDR:` prefix preserved.** Loanword acronym (matches fr-FR / es-MX / pt-BR).
- **`Shoutout` strings as `Un ringraziamento a X per Y.`** `Score Formula Shoutout` → `Un ringraziamento a MR_WEQ per aver fatto il reverse-engineering di questa formula del punteggio!`; `Score Range Shoutout` → `Un ringraziamento a daryen per aver raccolto i dati e finalizzato gli intervalli di punteggio per i voti in lettere!`. Names stay verbatim (`MR_WEQ`, `daryen`, `KyleTT`, `FEFEMZ`, `Team Infinitesimal`, `DrMurloc`).
- **Show / Hide as imperative `Mostra X` / `Nascondi X`.** Article (`il`/`la`/`i`/`le`/`lo`/`l'`) per gender and initial-letter rules of the modified noun. **Italian article rule reminder:** `lo` before masculine words starting with `s+consonant`, `z`, `gn`, `ps`, `x`, `y` (so `lo Step Artist`, `lo script`); `il` otherwise; `l'` before any vowel-initial noun (`l'utente`, `l'immagine`, `l'età`).
- **`Min` / `Max` postfixed with space, lowercase.** `BPM min`, `BPM max`, `Rating max`, `Conteggio note min`, `Voto in lettere min`. Don't switch to `Min. BPM` or prefix forms.
- **Possessives**: `il tuo account`, `i tuoi punteggi`, `la tua progressione`, `i tuoi follower`. Possessive precedes noun, takes the article, agrees in gender/number.
- **English brand and PIU jargon stay verbatim mid-sentence.** `Score Tracker`, `Phoenix`, `XX`, `PIU`, `PIUScores`, `PIUGame`, `Discord`, `Start.GG`, `Pass`, `ToDo`, `Charts`/`chart`, `CoOp`, `Co-Op`, `Singles`, `Doubles`, `Mix`, `Tier Lists`, `Plate`, `Rating`, `Step Artist`, `Stage Pass`, `Stage Break`, `BPM`, `UCS`, `Avatar`, `Tag`, `Tags` (kept English-spelled in the resx but treated as invariable in Italian prose), `Pumbility`, `PUMBILITY`, `Bounty`/`Bounties`, `Stamina`, `lifebar`, `folder`, `combo`, `run`, `play(s)`, `Spreadsheet`, `Seed`, `Bracket`, `Qualifiers`, judgment terms (`Bad`, `Miss`, `Perfect`, `Great`, `Good` and their English plurals `Bads`/`Misses`/`Perfects`/`Greats`/`Goods`), `JSON`, `CSV`, `TLDR`, `TRUE`/`FALSE`, `IsBroken`, `XXLetterGrade`, `ChartScoring`, `LetterDifficulties`, `dev-tools`, `debugging`, `hacky`, `data-mine`.
- **Tech loanwords are common.** `Carica` (verb) for upload, `Uploader` (noun) for the role; `Generatore casuale` for randomizer. Italian web/UI conventions accept loanwords; don't force native equivalents (`carpetta` for folder, `verifica di accesso` for login, `valutazione` for rating) where the loanword is more natural in this UI's voice.
- **Decimal separator: comma.** `0,5`, `9,6` in prose. Half-width Arabic numerals.
- **Article + apostrophe before vowels.** `l'utente`, `l'immagine`, `l'età`, `un'opzione`, `dell'account`, `nell'app`, `all'utente`. Italian feminine indefinite is `un'` with apostrophe; masculine indefinite is `un` without apostrophe (`un account`, `un utente`). Easy mistake to make — double-check.
- **Agreement reminders.**
  - `Charts` → masculine plural (`chart`): `chart completati`, `chart sugeriti`, `chart salvati`, `chart ripetuti`. Bare `chart` (singular) → masculine: `il chart`, `chart selezionato`.
  - `Tier List` → feminine (implicit `lista`): `Tier List calcolata`, `Tier List PIU`.
  - `UCS` → feminine (implicit `coreografia`).
  - `Lifebar` → feminine (implicit `barra`).

## Open decisions (terms upcoming batches will need)

These are terms the en-US source contains that don't yet have an obvious recommendation above. Decide once per term, then add the row to **Recommended term mappings** and use it consistently.

- **`Korean Name`** → `Nome coreano` (matches the pattern of other locales).
- **`Channel Name`** → `Nome del canale`.
- **`Discord Id`** → `Id Discord`.
- **`Youtube Hash`** → `Hash YouTube`.
- **`Estimated Point Gain Timeline`** → `Cronologia stimata dei guadagni di punti`.
- **`Custom Scoring Formula`** → `Formula di scoring personalizzata`.
- **`Best Attempts / Best Score`** → `Migliori tentativi / Miglior punteggio`.
- **`Bad Suggestion / Good Suggestion`** → `Suggerimento errato / Suggerimento corretto` (or `Cattivo suggerimento / Buon suggerimento` — first form reads more idiomatically).
- **`Doesn't Match My Personal Skills`** → `Non corrisponde alle mie abilità personali`.
- **`The Category Isn't Interesting to Me`** → `La categoria non mi interessa`.
- **`I Don't Like The Chart`** → `Non mi piace il chart`.
- **`I Just Want to Hide The Chart`** → `Voglio solo nascondere il chart`.
- **`Couldn't parse JSON`** → `Impossibile analizzare il JSON`.
- **`File cannot be larger than 10 MB`** → `Il file non può superare i 10 MB`.
- **`Players (Paste UserId from Account Page if not Public)`** → `Giocatori (incolla l'UserId dalla pagina account se non Pubblico)`.
- **`Player To Test (Must Be Set To Public)`** → `Giocatore da testare (deve essere impostato come Pubblico)`.
- **`Site constructed and maintained by DrMurloc`** → `Sito creato e mantenuto da DrMurloc`.
- **`Original Concept (excel score tracking) Constructed by KyleTT`** → `Concetto originale (tracciamento punteggi su Excel) realizzato da KyleTT`.
- **`Updated X Y` snackbar** → `{0} {1} aggiornato` (masculine default for the chart-save case).

## Process for the first batch

The first batch is structurally different from the per-locale ongoing batches because the resx and the supported-cultures wiring don't exist yet:

1. **Wire up the supported culture.** In [Program.cs](ScoreTracker/ScoreTracker/Program.cs), add `"it-IT"` to the supported-cultures array used by `AddRequestLocalization`. (Search for the existing `"fr-FR"` registration as the template.) Update the `Cross-cutting concerns` section in [ARCHITECTURE.md](ARCHITECTURE.md#cross-cutting-concerns) to list `it-IT` alongside the others.
2. **Create `App.it-IT.resx`** as a copy of `App.en-US.resx` with values cleared (or with English fallback values that this glossary will overwrite). Don't delete keys — the file must mirror the en-US key set exactly. Italian-localized values fill in over time; missing keys fall back to the English source per CLAUDE.md.
3. **Pick a feature folder** (Tournaments, Tier Lists, Progress, Admin, Tools, etc.) for the first content batch.
4. **Translate using this glossary.** If a new term needs a decision, add a row to `Recommended term mappings` **before translating** so the convention is captured.
5. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.
6. PR titled `Add it-IT localization scaffold + first batch (<Folder>)` for the first PR; subsequent PRs `Translate <Folder> to it-IT`.

## Process for ongoing batches (after the scaffold lands)

1. Pick a feature folder or a category from the recommendations.
2. List its English keys (`grep -oP '(?<=L\[")[^"]+' ScoreTracker/ScoreTracker/Pages/<Folder>/**/*.razor` or similar).
3. Cross-reference against `App.it-IT.resx` to find which are missing.
4. Translate using this glossary. **If a new term needs a decision, add a row to "Recommended term mappings" (or rename it to "Established term mappings" once the first content batch lands) before translating.**
5. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.
6. PR titled like `Translate <Folder> to it-IT`.

## Native review priority

Like the other machine-assisted locales (ja-JP, ko-KR, fr-FR, es-MX), Italian translations in this codebase will be machine-drafted by Claude until a native speaker reviews. Priority for native review:

- The Life Calculator and ChartScoring multi-paragraph prose entries (~30 dense paragraphs) — same caveat as every other locale.
- The PIU community jargon decisions in this glossary: `chart` masculine vs feminine, `Plate` untranslated vs `Placca`, `Mix` untranslated vs `Versione`, `Singles`/`Doubles` untranslated vs `Singolo`/`Doppio`, `Letter Grade` → `Voto in lettere` vs `Grado di lettera`, `Leaderboard` → `Classifica` vs untranslated.
- The register choice (`tu` informal). If a native speaker prefers `Lei` formal or a more neutral impersonal voice, document the decision and sweep the file in one batch — don't mix registers.

Short labels (column headers, button text) are likely fine without native review.
