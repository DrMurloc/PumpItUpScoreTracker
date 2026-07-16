# fr-FR localization glossary

Working reference for translating `App.en-US.resx` into `App.fr-FR.resx`. Captures the conventions established by the 601 translated entries (196 original + 405 from the 2026-04-26 bulk batch) so future batches stay consistent.

For the localization mechanism itself (resx layout, `L["..."]` usage, key conventions), see [ARCHITECTURE.md](ARCHITECTURE.md). For PIU domain terms in English, see [DOMAIN.md](DOMAIN.md). For the parallel ja-JP, ko-KR, and pt-BR conventions, see [LOCALIZATION-ja-JP.md](LOCALIZATION-ja-JP.md), [LOCALIZATION-ko-KR.md](LOCALIZATION-ko-KR.md), and [LOCALIZATION-pt-BR.md](LOCALIZATION-pt-BR.md).

## Style conventions

- **Address the user with formal `vous`.** Every existing prose string uses `vous` / `votre` / `vos`, never `tu`. Examples: `Si vous n'avez pas de compte`, `Vous serez retiré de la communauté Monde`, `Vérifiez bien que vos bloqueurs de publicité sont désactivés`. Match that — French web UI default is `vous` and the existing voice is consistent on this one point.
- **Capitalization in values is inconsistent.** Native French orthography uses **sentence case** (only the first word capitalized, plus proper nouns). The worst Title Case offenders (`Statut du Score`, `Mon Score`, `Prochaine Lettre`, `Charts Sauvegardées`, `Catégorisation par Difficulté`, `Politique de Confidentialité Complète`, `Type de Chanson`, `Enregistré Le`) were swept to sentence case in the 2026-07 QC pass, but plenty of Title Case remains (`Sauvegarder les Scores`, `Nombre de Charts`, `Très Facile`, …). Use sentence case for new entries; the full-file sweep is still an open item — see Known issues.
- **Brand and proper-noun casing is preserved verbatim.** `Score Tracker`, `Phoenix`, `XX`, `PIU`, `PIUScores`, `Discord`, `Start.GG`, `https://piugame.com` — keep their original casing inside French sentences.
- **Preserve positional placeholders verbatim.** `{0}`, `{1}`, `{2}` go into the value untouched, in whatever order French grammar wants. Examples: `Se connecter avec {0}`; `{0}/{1} Uploadés. {2} Restants. {3} Problèmes d'enregistrement`; `{0} Charts Restantes pour Vous`; `Vous avez {0} charts de niveau {1} dans votre ToDo list que vous n'avez pas Pass`.
- **Skip prose with inline markup.** Per CLAUDE.md, `<MudText>` bodies with embedded `<MudLink>`/other elements stay hardcoded English. Don't extract them, don't translate them.
- **Use proper French orthography.** Acute, grave, circumflex, cedilla, diaeresis — `àâäéèêëîïôöùûüç`. Don't ASCII-fold. The existing file already uses `À`, `é`, `è`, `ê`, `ç`, `î`, `ô`, `û` correctly in most places (see Known issues for a few stripped-accent typos).
- **French typographic spacing is not currently enforced.** Standard French typography requires a non-breaking space (`U+00A0`) before `?`, `!`, `:`, `;`, and inside `« »`. The existing file is inconsistent: `Création de compte?` (no space), `succès !` (regular space), `envoi !  Assurez vous` (regular space + double space). Do not introduce new violations; preferably normalize to NBSP before high punctuation, but flag rather than churn the whole file in one pass — see Known issues.

## Established term mappings

These have at least one existing translation in `App.fr-FR.resx`. New translations of the same term must reuse the established form unless there's a documented reason to change it.

### App / generic UI

| English | fr-FR | Notes |
|---|---|---|
| Score Tracker (the app) | Score Tracker | Brand name, kept English. Used inside French sentences as-is. |
| About | À propos | |
| Account | Compte | |
| Account Creation? | Création de compte? | Note: should be `Création de compte ?` with NBSP per French typography. |
| Actions | Actions | Identical in both languages. |
| Add | Ajouter | |
| Add UCS | Ajouter une UCS | Article `une` because UCS treated as feminine (la chorégraphie). |
| Add to Favorites | Ajouter aux favoris | |
| Add to ToDo | Ajouter à la ToDo List | `ToDo List` kept English mid-sentence; article `la` (feminine). |
| Age | Ancienneté | Rendered as "seniority/longevity" rather than literal age, fitting the chart-age column context. |
| Avatar | Avatar | Loanword. |
| Average | Moyenne | |
| Broken | Cassé | Past participle, masculine default. |
| Cancel | Annuler | |
| Chart Count | Nombre de Charts | `Charts` stays English (per chart-untranslated rule); modifier translated. Note Title Case on `Charts`. |
| Close | Fermer | |
| Communities | Communautés | |
| Completed | Terminé | Masculine default. "Hide Completed Charts" → "Masquer les Charts terminées" (feminine plural agreement with `Charts`). |
| Copy Script | Copier le Script | Title Case on `Script`. |
| Copy to Clipboard | Copier dans le presse-papiers | |
| Country | Pays | |
| Difficulty Level | Niveau de difficulté | Sentence case. |
| Download Failures | Échecs de téléchargement | Capital É at sentence start (`Échecs d'analyse des données` likewise). |
| Download Scores | Télécharger les scores | `Télécharger` = download only; upload is `Téléverser` (see Upload Scores). |
| Easiest Player | Joueur le plus facile | Comparative. Feminine form would be `Joueuse la plus facile` if needed. |
| Easy / Hard | Facile / Difficile | Plain adjectives, masculine default. |
| Ending Page | Page de fin | "Starting Page" → "Page de début". |
| Event | Évènement | |
| Event Links | Liens Évènements | Note: `Liens Évènements` lacks `des`/`d'` — `Liens d'évènements` would be more standard. Acceptable as a label. |
| Favorites | Favoris | |
| Filters | Filtres | |
| Full Privacy Policy | Politique de confidentialité complète | |
| Home | Accueil | Sidebar nav label. |
| Language | Langue | |
| Last Updated | Dernière mise à jour | |
| Level | Niveau | |
| Link | Lien | |
| Login | Connexion | Noun. Distinct from "Log In With" verb form. |
| Log In With | Se connecter avec {0} | Verb form. Placeholder takes the provider name (Discord, Google, Facebook). |
| Logout | Déconnexion | |
| Make Public / Private | Rendre public / Rendre privé | Verb construction. Sentence case. |
| Max Rating | Rating Max | `Rating` stays English (loanword); `Max` postfixed. Matches the `BPM Min`/`BPM Max` pattern. |
| Medium | Moyen | Masculine default. |
| Min/Max BPM | BPM Min / BPM Max | Postfixed abbreviation. |
| Min/Max Letter Grade | Rang Min (lettres) / Rang Max (lettres) | Postfixed `Min`/`Max`; the `(lettres)` parenthetical disambiguates `Rang` (which alone could mean a numeric rank). |
| Min/Max Note Count | Nombre de Notes Min / Nombre de Notes Max | Postfixed. |
| My Score | Mon score | |
| Name | Nom | |
| Next Letter | Prochaine lettre | "Next" rendered as `Prochaine` (feminine, agreeing with `lettre`). |
| Not Graded Count | Non Gradé | "Not Graded" with `Count` left implicit. Masculine default. |
| Open | Ouvert | Masculine default. |
| Pending | En attente | |
| Photos | Photos | Identical. |
| Place (rank) | Place | "1st place" sense. |
| Players | Joueurs | Masculine default plural. |
| Popularity | Popularité | |
| Progress | Progression | |
| Public | Publique | Note: written as `Publique` (feminine) even though referenced as a generic on/off label. Standard masculine would be `Public` — `Publique` is the feminine form. The label likely refers to "[ta] visibilité publique" but as a bare adjective it should agree with whatever it modifies. See Known issues. |
| Recorded Date | Date d'enregistrement | |
| Recorded On X | Enregistré le {0} | |
| Remove from ToDo | Enlever de la ToDo List | Matches "Add to ToDo" → "Ajouter à la ToDo List". |
| Report Video | Signaler vidéo | |
| Report Video Tooltip | Signaler lien endommagé, ou vidéo incorrecte | |
| Restart | Recommencer | |
| Rules | Règles | |
| Save Scores | Sauvegarder les Scores | Title Case on `Scores` — still un-swept (full sentence-case sweep is an open item). |
| Saved Charts | Charts sauvegardées | Feminine plural agreement — `Charts` is committed feminine file-wide (the old masculine `Restants` outliers were swept to `Restantes`). |
| Score | Score | Identical, loanword. |
| Score (Data Backed) | Score (Avec Données) | The English key suffix `(Data Backed)` is rendered as `(Avec Données)`. The orphan entry keyed by its own French value (`Pass (Avec Données)`) was deleted in the 2026-07 QC pass. |
| Score Loss | Perte de Score liée aux {0} | Note: adds `liée aux` before placeholder (assumes feminine plural — `liée` agrees with `Perte`, `aux` with judgment-term plural). For singular or masculine placeholders the agreement may be wrong. See Known issues. |
| Score Ranking | Classement par Score | Compare `Leaderboard` → `Leaderboard` (loanword); `World Rankings` → `Classements mondiaux`. Three different renderings for the ranking family. |
| Score State | Statut du score | |
| Scores Parsed | Scores Analysés | |
| Settings | Réglages | |
| Show X | Montrer X | Suffix pattern: `Show Skills` → `Montrer les Compétences`, `Show Difficulty` → `Montrer la Difficulté`, `Show Song Name` → `Montrer le Nom de la Chanson`, `Show Step Artist` → `Montrer le Step Artist`, `Show Age` → `Montrer l'ancienneté`. Article (`les`/`la`/`le`/`l'`) per gender of the modified noun. |
| Show Score Distribution | Montrer la distribution des scores | Converged on `Montrer` (was the `Afficher` outlier). |
| Show Only ToDo Charts | Montrer seulement les Charts ToDo | `ToDo` kept English as the chart-state qualifier. |
| Site | Site | (Implicit in "Site web".) |
| Skill | Compétence | Chart trait/skill (runs, drills, twists, etc.). |
| Submission Page | Page d'envoi | |
| Submit | Envoyer | Standard French for form submit. |
| Suggested Chart | Chart Suggérée | Feminine agreement (`Charts`/`Chart` treated as feminine here). |
| Tag / Tags | Tag / Tags | Loanwords. |
| Text View | Vue Textuelle | |
| Tier Lists | Tier Lists | Untranslated. |
| Title Progress | Progression des titres | |
| Titles | Titres | |
| To Do | À Faire | "ToDo List" / "ToDo Charts" stay English in compounds, but bare "To Do" → `À Faire`. |
| To Leaderboard | Vers le Leaderboard | Nav label pointing at the leaderboard page. |
| Tools | Outils | |
| Total Count | Total | `Count` left implicit. |
| Tournaments | Tournois | |
| UCS Leaderboard | Leaderboard UCS | Postfixed UCS. |
| Upload Image | Uploader l'image | Loanword verb `uploader`. |
| Upload Scores | Téléverser les scores | `Téléverser` = upload (the old `Télécharger` wrong-direction values were fixed in the 2026-07 QC pass). |
| Upload XX Scores | Téléverser les scores XX | |
| Uploader | Uploadeur | Role/agent noun derived from `uploader`. |
| Use Script | Utiliser le script | |
| Used Primarily for debugging | Utilisé principalement pour déboguer | |
| Username | Nom d'utilisateur | |
| Validation | Validation | Identical. |
| Very Easy / Very Hard | Très Facile / Très Difficile | Title Case. |
| Video | Vidéo | "Open Video" → "Ouvrir Vidéo" (Title Case, missing article — `Ouvrir la vidéo` would be more natural). |
| Vote Count | {0} votes | Placeholder + lowercase `votes`. |
| Website | Site web | |
| World Rankings | Classements mondiaux | |
| 1+ Level Easier / Harder | 1+ Niveau Plus Facile / 1+ Niveau Plus Difficile | Title Case. Plural agreement would normally make `Niveaux` for "1+ levels", but bare singular reads as a difficulty-shift label. |

### PIU domain

| English | fr-FR | Notes |
|---|---|---|
| Chart(s) | Chart / Charts | **Untranslated**, kept English. Used compositionally with French articles: `Type de Chart`, `Liste des Charts`, `Charts sauvegardées`, `Charts Restantes`, `Masquer les Charts`. Feminine file-wide. |
| Chart List | Liste des Charts | |
| Chart Randomizer | Randomizer de Chart | `Randomizer` kept English (no native French equivalent in use). |
| Chart Type | Type de Chart | |
| CoOp | CoOp | Untranslated. "CoOp Aggregation" → "Agrégation des CoOp". |
| Singles / Doubles | Singles / Doubles | Untranslated. |
| Difficulty Level | Niveau de difficulté | Sentence case. |
| Difficulty Categorization | Catégorisation par difficulté | |
| Letter Grade | Rang (lettres) | Translated as `Rang` with parenthetical `(lettres)` to disambiguate from numeric rank. Min/Max forms keep the parenthetical. |
| Mix | Mix | Untranslated. (Compare ja-JP `バージョン`/`ベーション`, ko-KR `시리즈`, pt-BR `Versão`.) |
| Phoenix / XX | Phoenix / XX | Game versions, untranslated proper nouns. "Phoenix Score Calculator" → "Calculateur de score Phoenix" (Phoenix postfixed). "Import Phoenix Scores" → "Importer les Scores Phoenix"; "Upload XX Scores" → "Télécharger Scores XX". |
| Plate | Plaque | **Translated** — `Plaque` is a literal French rendering. (Compare ja-JP `プレート` loanword, pt-BR `Plate` untranslated, ko-KR `플레이트` loanword.) French is the only locale that translates this. The comment notes `MG, PG, UG` are the in-game tier abbreviations. |
| Pass / Passed / Not Passed | Pass / Passed / Non Passed | **Untranslated**, kept English. `Passed Count` → `Passed`; `Not Passed Count` → `Non Passed`; `Stage Pass` → `Stage Pass`. The `Unpassed ToDos` prose keeps `Pass` in English mid-French-sentence: `que vous n'avez pas Pass`. |
| Score (singular) | Score | Identical, loanword. |
| Scores (plural) | Scores | Identical. |
| Score State | Statut du Score | The comment lists `Passed, Unpassed, Unscored` as state values — those stay English. |
| Phoenix Score Calculator | Calculateur de score Phoenix | Sentence case. |
| Score Loss | Perte de Score liée aux {0} | See generic UI table. |
| Note Count | Nombre de Notes | |
| BPM | BPM | Untranslated. Min/Max forms postfix the modifier: `BPM Min`, `BPM Max`. |
| Step Artist | Step Artist | Untranslated loanword. (Compare pt-BR `Autor dos passos`.) |
| Tier Lists | Tier Lists | Untranslated. |
| Leaderboard | Leaderboard | **Untranslated** when bare. Used in compounds: `Leaderboard UCS`, `Vers le Leaderboard`, `Leaderboard de Qualification`, `Comparaison de Joueurs sur Leaderboard`. Compare `World Rankings` → `Classements mondiaux` and `Score Ranking` → `Classement par Score` — `Leaderboard` and `Classement(s)` co-exist for different keys. |
| Communities | Communautés | |
| Country | Pays | |
| UCS | UCS | Untranslated acronym. "Add UCS" → "Ajouter une UCS"; "UCS Leaderboard" → "Leaderboard UCS". Treated as feminine (la chorégraphie utilisateur). |
| Players | Joueurs | Masculine plural default. |
| Player Count | Nombre de Joueurs | |
| Tournaments | Tournois | |
| Qualifiers | qualifications | Converged plural lowercase: "Qualifiers Leaderboard" → "{0} Leaderboard de qualifications"; "Qualifiers Submission" → "{0} envois pour qualifications"; "Sync Qualifier Leaderboard" → "Synchroniser le Leaderboard de qualifications". |
| Rating | Rating | **Untranslated**. Loanword. "Max Rating" → "Rating Max"; "Rating Calculator" → "Calculateur de Rating". |
| Rating Calculator | Calculateur de Rating | |
| Pumbility | (none yet) | Not yet translated. Recommend leaving as `Pumbility` (proper-noun loanword) per the Phoenix/XX/Rating policy. |
| Song | Chanson | (Compare pt-BR `Música`, ko-KR `노래/음악`, ja-JP `曲`.) |
| Song Name | Nom de la chanson | |
| Song Image | Image de la chanson | |
| Song Duration | Durée de la chanson | |
| Song Type | Type de chanson | |
| Song Artist | Artiste de la chanson | Matches the other Song compounds. |
| Personalized Difficulty | Difficulté Personnalisée | |
| Scoring Level | Niveau de Score | |
| Skill | Compétence | |
| Stage Pass | Stage Pass | Untranslated. |
| Suggested Chart | Chart Suggérée | |
| Title (in-game title award) | Titre | "Title Progress" → "Progression des titres"; "Titles" → "Titres". |
| Favorites | Favoris | "Add to Favorites" → "Ajouter aux favoris". |
| Progress Charts | Statistiques Joueur | nabulator-style interpretive rendering ("player statistics" rather than "progress charts"). Title Case. Loses the literal meaning — see Known issues. |
| Player Stats | (covered by Progress Charts) | |
| Avatar | Avatar | |

### Game-mechanic vocabulary

| English | fr-FR | Notes |
|---|---|---|
| Broken | Cassé | Past participle, masculine default. |
| Pass / Passed / Not Passed | Pass / Passed / Non Passed | (Cross-reference: PIU domain.) |

## Phrasing patterns to copy

- **Formal `vous` register.** Every prose string uses `vous` / `votre` / `vos`. Don't introduce `tu`. Example: `Si vous n'avez pas de compte avec Score Tracker, un compte sera créé lorsque vous sélectionnez une des méthodes d'authentification.`
- **`NB :` or `NB:` prefix for warnings/disclaimers.** Used for technical notes: `NB : la perte de score peut être incorrecte de 1 à 4 points à cause de l'arrondi`, `NB: cet outil est expérimental...`. Place at sentence start. Spacing inconsistent (`NB :` vs `NB:`) — pick one and standardize. French typography wants `NB :` with NBSP.
- **`Shout out à X pour Y` for credit lines.** `Shout out à MR_WEQ pour le reverse-engineering de la formule !`, `Shout out à daryen pour la collecte de données et la finalisation des intervalles de scores pour les lettres de Rang !`. Loanword `Shout out` kept English; `à` introduces the credited person.
- **Show / Hide as verb-prefix `Montrer X` / `Masquer X`.** `Show Skills` → `Montrer les Compétences`, `Hide Completed Charts` → `Masquer les Charts terminées`. Article (`les`/`la`/`le`/`l'`) per gender of the modified noun. The old `Afficher` variants were converged to `Montrer` in the 2026-07 QC pass — `Montrer` only for new entries.
- **`Min` / `Max` postfixed with space.** `BPM Min`, `BPM Max`, `Rating Max`, `Nombre de Notes Min`, `Rang Min (lettres)`. Don't switch to `Min. BPM` or prefix forms.
- **Possessives**: `votre compte`, `vos scores`, `votre progression`, `vos followers`. Possessive precedes noun, agrees in gender/number.
- **English brand and PIU jargon stay verbatim mid-sentence.** `Score Tracker`, `Phoenix`, `XX`, `PIU`, `PIUScores`, `Discord`, `Start.GG`, `Pass`, `ToDo`, `Charts`, `CoOp`, `Singles`, `Doubles`, `Mix`, `Tier Lists`, `Leaderboard`, `Rating`, `Step Artist`, `Stage Pass`, `BPM`, `UCS`, `Avatar`, `Tag`, `Tags`, judgment terms (`Bad`, `Miss`, `Perfect`, `Great`, `Good`, plural `Goods`).
- **Tech loanwords** are common: `uploader` (verb), `Uploadeur` (noun), `Randomizer`, `Console de Développement`. Don't replace them with native French alternatives (`téléverser`, `générateur aléatoire`, `console de développeur`) without a deliberate sweep — they read more naturally in this UI's voice.

## Known issues / native review needed

The mechanical items originally tracked here were fixed in the 2026-07 QC pass:

- **Critical**: the orphan `<data name="Pass (Avec Données)">` entry (keyed by its own French value, resolvable by no `L["..."]` call) was deleted.
- **Wrong-direction**: `Upload Scores` / `Upload XX Scores` now use `Téléverser les scores` / `Téléverser les scores XX`; `Télécharger` remains download-only. (`Upload Image` had always been correct with `Uploader l'image`.)
- **Spelling**: `Agrégation des CoOp` (was `Aggregation des CoOps`), `précédemment`, `sûrement`, `déjà été sauvegardés` (accent + the missing `été`), `score` (was `scopre`), `requêtes` and `développeur` (both in Use Password 3), `Échecs` ×2, `utiliser` (was `utilsier`), `Assurez-vous` (hyphen + the double space before it), `ajouté à la communauté Monde` (was `de`), and the `Vousu` typo in the Remaining Charts For You comment.
- **Capitalization**: the worst offenders swept to sentence case (`Politique de confidentialité complète`, `Statut du score`, `Mon score`, `Prochaine lettre`, `Catégorisation par difficulté`, `Charts sauvegardées`, `Type de chanson`, `Enregistré le {0}`).
- **Charts gender**: committed **feminine**; the masculine `Charts Restants` outliers swept to `Charts Restantes` (both Remaining-Charts keys and their comments).
- **Show verb**: converged on `Montrer` (the two `Afficher` entries rewritten).
- **Qualifiers number**: converged on plural lowercase `qualifications` (Leaderboard, Submission, and Sync entries).
- **`Song Artist`**: translated to `Artiste de la chanson`.

Still open for a native/owner pass:

### Full sentence-case sweep

Only the worst Title Case offenders were fixed. Plenty remains (`Sauvegarder les Scores`, `Nombre de Charts`, `Nombre de Notes Min/Max`, `Très Facile / Très Difficile`, `Ouvrir Vidéo`, `Chart Suggérée`, `Statistiques Joueur`, `Vue Textuelle`, `Classement par Score`, …). Standard French is **sentence case**; a one-shot sweep to lowercase non-initial, non-proper-noun words is still recommended — separate diff, not mixed into a feature batch.

### Questionable word choices

- **`Public` → `Publique`.** `Publique` is the feminine singular form. As a generic on/off label whose grammatical antecedent isn't in the key, the masculine `Public` would be safer. The comment (`(on/off, si le compte est configuré en tant que publique)`) implies the antecedent is `[la visibilité]`, justifying feminine — but as a bare label this is fragile. Compare `Make Public` → `Rendre public` (masculine), so the file is internally split.
- **`Score Loss` → `Perte de Score liée aux {0}`.** Adds `liée` (feminine, agrees with `Perte`) and `aux` (plural). Works for plural masculine judgment terms (`Goods`, `Greats`, `Bads`, `Misses`). For singular or ambiguous placeholders the agreement may be wrong. The English source is `{0} Score Loss` (e.g. "Goods Score Loss") — known to be plural in current usage, so this is currently safe but fragile.
- **`Progress Charts` → `Statistiques Joueur`.** "Player statistics" rather than literal "progress charts" — interpretive, like nabulator's ja-JP `進捗中の譜面` for `Player Stats`. May or may not be the right call depending on the page-context label.
- **`Plate` → `Plaque`.** Literal French translation. Other locales (ja-JP, ko-KR, pt-BR, it-IT) keep `Plate` or use a phonetic loanword. The PIU community in France probably uses `Plate` in conversation; `Plaque` reads as an over-translation. Consider switching to `Plate`. The comment lists `MG, PG, UG` (in-game tier names) which stay untranslated regardless.

### Typographic spacing

French typography requires a non-breaking space (`U+00A0`, `&#160;` in resx) before `?`, `!`, `:`, `;`, and inside `« »`. Existing file is inconsistent:

- `Création de compte?` — no space (should be `Création de compte ?`)
- `succès !` — regular ASCII space (should be `succès !`)
- `C'est votre premier envoi ! Assurez-vous` — regular ASCII space (should be `envoi ! Assurez-vous` with NBSP)
- `NB : la perte` — regular space (should be `NB :`)
- `NB:` — no space (inconsistent with `NB :`)

Recommend a one-shot sweep to insert NBSP before all `?`, `!`, `:`, `;`. Don't mix into a feature batch.

## 2026-04-26 bulk batch — decisions and additions

The 405 entries added on 2026-04-26 made structural choices that should be honored by future batches. Most of these are *new* established mappings (now in effect, but added below as a single block rather than retro-fitted into the long tables above to keep the diff reviewable). A few are deliberate divergences from the pre-existing file's conventions that future cleanup batches should propagate to the older entries.

### Conventions committed by the bulk batch

- **`Charts` is feminine.** All new agreement-bearing entries treat `Chart`/`Charts` as feminine (`Chart sauvegardée`, `Chart sélectionnée`, `Charts répétées`). This aligns with the older entries (`Charts sauvegardées`, `Chart Suggérée`, `Charts terminées`); the last masculine outliers (`Charts Restants`) were swept to `Charts Restantes` in the 2026-07 QC pass.
- **Sentence case is the new default.** New entries use sentence case throughout (`Niveau Min`, `Score moyen`, `Détails du chart`, `Distribution des plaques`). The older Title Case entries are not retro-fixed in this batch.
- **`Show` → `Montrer` only.** `Afficher` was not used in any new entry.
- **`Hide` → `Masquer`.**
- **English typographic spacing.** New entries use ASCII space + `:` / `?` / `!` (e.g. `Score : {0}`, `Que devrais-je jouer ?`, `succès !`). NBSP normalization is still pending — a separate sweep should handle the whole file at once.
- **PIU jargon stays English mid-sentence.** Confirmed for: `Pass`, `Passed`, `Step Artist`, `Spreadsheet`, `Stage Break`, `lifebar`, `bad`, `miss`, `perfect`, `great`, `good`, `combo`, `run`, `play(s)`, `Rainbow Life`, `Stage Break`, `Stamina`, `Bounty/Bounties`, `Seed`, `folder`, `JSON`. Lowercase when used as a generic noun in prose; capitalized when standalone label or proper-noun-feeling.
- **`Combo X / X Break` compound headers.** "Perfect Combo, Miss Break" → `Combo Perfect, Miss Break` (French word order, English judgment + break terms verbatim). Same for the four-cell matrix.
- **`Lifebar` lowercase loanword.** `lifebar` mid-prose, `Lifebar` at start of label (e.g. `Calculateur de lifebar`, `Description de la lifebar`, `Statistiques de lifebar`).
- **`folder` lowercase loanword** for the PIU difficulty-level group (`folder cible`, `Distribution pondérée par folder`, `Moyennes par folder`). Not capitalized, not translated as `Dossier`.
- **`Tier List` is feminine.** `Tier List calculée`, `Tier List PIU`. Feminine because of the implicit `liste`.
- **`Plate` → `Plaque(s)` continued.** New entries `Distribution des plaques`, `Détail des plaques`, `Plaques`, `Plaque moyenne` honor the existing `Plaque` translation. (The pre-batch glossary flagged this as questionable vs. PIU community usage of `Plate` — decision deferred; `Plaque` remains in force.)
- **`Tournament` → `Tournoi` / `du tournoi`.** `Nom du tournoi`, `Paramètres du tournoi`, `Rôle dans le tournoi`, `Dates du tournoi`, `Settings du tournoi`. Compounds use `du tournoi`, not `de tournoi`.
- **`PUMBILITY` (uppercase) preserved.** When the source has `PUMBILITY` in caps, the value mirrors that. Lowercase `Pumbility` would be used elsewhere (none yet).
- **`Leaderboard` reserved for the `Leaderboard` keyword.** New compound forms: `Leaderboard mensuel`, `Leaderboard du chart`, `Leaderboard des Bounties`, `Leaderboards officiels`, `Leaderboards de complétion`, `Leaderboards UCS`. `Classement(s)` is reserved for `Score Ranking(s)` / `World Rankings`.
- **Decimal separator: comma.** `0,5`, `9,6` in prose. Half-width Arabic numerals.
- **Critical-bug fix shipped.** Added correct `Pass (Data Backed)` resx entry. (The orphan broken-key entry it replaced was deleted in the 2026-07 QC pass.)

### Established term mappings added 2026-04-26

These are now in effect. New entries in future batches should reuse them.

#### PIU domain

| English | fr-FR | Notes |
|---|---|---|
| Bounty / Bounties | Bounties | Untranslated. `Bounty Leaderboard` → `Leaderboard des Bounties`. |
| BPM | BPM | (Already established; reaffirmed.) |
| Calculated Tier List | Tier List calculée | Feminine. |
| Chart (singular) | Chart | Already established; gender now committed feminine. `Save Chart` → `Sauvegarder la chart`; `Chart saved` → `Chart sauvegardée`. |
| Chart Average | Moyenne du chart | |
| Chart Compare | Comparaison de charts | |
| Chart Count By Level | Nombre de charts par niveau | |
| Chart Details | Détails du chart | |
| Chart Difficulty by Letter Grade | Difficulté du chart par rang (lettres) | |
| Chart Leaderboard | Leaderboard du chart | |
| Chart Score | Score du chart | |
| Chart Statistics | Statistiques du chart | |
| Chart Update | Mise à jour de chart | |
| ChartScoring (page) | ChartScoring | Page-name source has no space; preserved verbatim. |
| Co-Op (with hyphen) | Co-Op | Distinct from `CoOp` (without hyphen) — keep both verbatim. |
| Combined | Combiné | |
| Community Completion | Complétion communautaire | |
| Competitive Level | Niveau compétitif | |
| Competitively | Compétitivement | |
| Completion | Complétion | |
| Doubles Level | Niveau Doubles | Postfixed `Doubles`. |
| Folder (PIU difficulty group) | folder | Lowercase loanword. Not `Dossier`. |
| Folder Averages | Moyennes par folder | |
| Folder Weighted Distribution | Distribution pondérée par folder | |
| Lifebar / lifebar | lifebar | Lowercase loanword in prose; `Lifebar` capitalized only at start of a label. |
| Lifebar Calculator | Calculateur de lifebar | |
| Lifebar Description | Description de la lifebar | |
| Lifebar stats | Statistiques de lifebar | |
| Letter Difficulty | Difficulté par lettre | Same rendering as `Difficulty By Letter`. |
| LetterDifficulties (page) | LetterDifficulties | Page-name no-space preserved. |
| Life Threshold | Seuil de vie | |
| Life Bar by Level | Lifebar par niveau | |
| Max Life | Vie max | |
| Min Score / Avg Score / Max Score | Score Min / Score moyen / Score Max | |
| Min Level / Max Level | Niveau Min / Niveau Max | Postfixed; sentence case with capitalized `Min`/`Max`. |
| Note Count | Nombre de notes | (Reaffirmed; existing `Nombre de Notes` Title Case is the older form.) |
| Note Counts (plural) | Nombres de notes | |
| Official Leaderboards | Leaderboards officiels | |
| Pass | Pass | (Reaffirmed; loanword.) |
| Passed / Unpassed | Passed / Non Passed | (Reaffirmed.) |
| Passes (plural) | Passes | English plural mirrored. |
| Pass Rate | Taux de Pass | |
| Passes by Competitive Level | Pass par niveau compétitif | |
| Passes By Level | Pass par niveau | |
| PIU Life Calculator | Calculateur de vie PIU | |
| PIU Tier List | Tier List PIU | Postfixed `PIU`. |
| Plate Breakdown / Plate Distribution / Plates / Avg Plate | Détail des plaques / Distribution des plaques / Plaques / Plaque moyenne | Continues `Plate` → `Plaque` mapping. |
| Play Count / X Plays | Nombre de plays / `{0} plays` | Lowercase `plays` loanword. |
| PUMBILITY | PUMBILITY | Uppercase preserved. |
| Run (a play-through) | run | Lowercase loanword in prose. |
| Score Distribution | Distribution de scores | |
| Score Distribution Lines | Lignes de distribution de scores | |
| Score Distribution By Player Level | Distribution de scores par niveau de joueur | |
| Score Rankings (plural) | Classements par score | Sentence case, plural. (Existing `Score Ranking` singular → `Classement par Score` Title Case is the older form.) |
| Scoring Difficulty | Difficulté de scoring | |
| Scoring Level | Niveau de score | (Older `Niveau de Score` Title Case kept; sentence case for new entries.) |
| Scoring Rankings | Classements par scoring | |
| Selected Chart | Chart sélectionnée | Feminine. |
| Similar Players | Joueurs similaires | |
| Singles Level | Niveau Singles | Postfixed `Singles`. |
| Singles vs Doubles | Singles vs Doubles | Identical. |
| Spreadsheet | Spreadsheet | Untranslated loanword. |
| Stage Break Modifier | Modificateur de Stage Break | `Stage Break` kept English. |
| Stamina | Stamina | Untranslated loanword. |
| Stamina Session Builder | Constructeur de session Stamina | |
| Starting Life | Vie de départ | |
| Step Artist (singular) | Step Artist | Reaffirmed loanword (already established). |
| Step Artists (plural) | Step Artists | Plural loanword. |
| Suggested Chart (already) | Chart Suggérée | (Already established.) |
| Tier List (singular) | Tier List | Feminine. (Existing plural `Tier Lists` is also untranslated; same family.) |
| Top 50 X | Top 50 {0} | |
| Tournament | Tournoi | |
| Tournament Name / Tournament Settings / Tournament Role / Tournament Dates | Nom du tournoi / Paramètres du tournoi / Rôle dans le tournoi / Dates du tournoi | `du tournoi` compound. |
| UCS Leaderboards | Leaderboards UCS | Postfixed UCS. |
| Ungraded | Sans note | (Reaffirmed; matches `Sem nota` family in pt-BR.) |
| Visible Life | Vie visible | |
| Weekly Charts | Charts hebdomadaires | |
| What Should I Play / ? | Que devrais-je jouer / ? | Both with-and-without-`?` source variants are present. |
| XX Progress | Progression XX | No article (matches pt-BR pattern, sidesteps gender). |

#### Tournament / competition

| English | fr-FR | Notes |
|---|---|---|
| Active / Upcoming / Previous Tournaments | Tournois actifs / Tournois à venir / Tournois précédents | |
| Always / Never (date fallbacks) | Toujours / Jamais | |
| Brackets (tournament) | Tableaux | `{0} Brackets` → `Tableaux de {0}` (reordered for French structure). |
| End Date / Start Date | Date de fin / Date de début | |
| In Person | En personne | |
| Location | Lieu | |
| Machines / Machine Name | Machines / Nom de la machine | |
| Player Name / New Player Name | Nom du joueur / Nom du nouveau joueur | |
| Players have X to play charts. … | Les joueurs disposent de {0} pour jouer des charts. … | |
| Qualifier Leaderboard (verb form `Sync …`) | Synchroniser le Leaderboard de qualification | |
| Repeated charts X allowed. | Les charts répétées {0} autorisées. | `{0}` = `sont` / `ne sont pas` (separately localized). |
| Seed | Seed | Untranslated. |
| Tournament Role | Rôle dans le tournoi | |

#### App / generic UI

| English | fr-FR | Notes |
|---|---|---|
| (Optional) Also delete historical data | (Optionnel) Supprimer aussi les données historiques | |
| Additional Comments | Commentaires supplémentaires | |
| Admin | Admin | Loanword/role name. |
| Admin Settings | Paramètres admin | |
| All | Tous | Masculine plural default. |
| Allow Repeats | Autoriser les répétitions | |
| Anonymous | Anonyme | |
| are / are not | sont / ne sont pas | Used as substitution into `Repeated charts X allowed`. |
| Average Difficulty | Difficulté moyenne | |
| Bad Suggestion / Good Suggestion | Mauvaise suggestion / Bonne suggestion | |
| Best Attempts / Best Score | Meilleures tentatives / Meilleur score | |
| Build Session | Construire la session | |
| Bulk Vote | Vote en masse | |
| Category | Catégorie | |
| Channel Name | Nom de la chaîne | |
| Clear Cache | Vider le cache | |
| Combined | Combiné | |
| Community Invite | Invitation à la communauté | |
| Competition | Compétition | |
| Confirm | Confirmer | |
| Content Lock | Verrou de contenu | |
| Copied to clipboard! | Copié dans le presse-papiers ! | |
| Could not find chart / song | Chart introuvable / Chanson introuvable | |
| Couldn't parse JSON | Impossible d'analyser le JSON | |
| Create | Créer | |
| Create Song | Créer une chanson | |
| Current | Actuel | |
| Current Username / New Username | Nom d'utilisateur actuel / Nouveau nom d'utilisateur | |
| Custom Scoring Formula | Formule de score personnalisée | |
| Default | Par défaut | |
| Delete / Delete All Scores | Supprimer / Supprimer tous les scores | |
| Description | Description | |
| Difficulty | Difficulté | |
| Difficulty By Letter | Difficulté par lettre | |
| Difficulty By Player Level | Difficulté par niveau de joueur | |
| Difficulty Letters / Difficulty Passes / Difficulty Progress | Lettres par difficulté / Pass par difficulté / Progression par difficulté | |
| Difficulty Range | Plage de difficulté | |
| Discord Id | Id Discord | |
| Do It | Faire | Admin trigger button. |
| Doesn't Match My Personal Skills | Ne correspond pas à mes compétences personnelles | |
| Done | Terminé | Same as `Completed`. |
| Download Example | Télécharger un exemple | |
| Duration | Durée | |
| Edit | Modifier | |
| Effective Level | Niveau effectif | |
| Estimated Point Gain Timeline | Chronologie estimée de gain de points | |
| Example Set Builder | Constructeur de set d'exemple | |
| Existing | Existant | |
| Extra Settings | Paramètres supplémentaires | |
| File cannot be larger than 10 MB | Le fichier ne peut pas dépasser 10 Mo | |
| Final Result / Final Result: X | Résultat final / `Résultat final : {0}` | |
| From / To (mapping) | De / Vers | |
| Game Stats | Statistiques du jeu | |
| Hide | Masquer | |
| Hide Chart for this Category | Masquer le chart pour cette catégorie | |
| Hide Record-less Charts / Hide Zero Scoring Charts | Masquer les charts sans record / Masquer les charts à zéro | |
| I Don't Like The Chart | Je n'aime pas ce chart | |
| I Just Want to Hide The Chart | Je veux juste masquer ce chart | |
| Image Name | Nom de l'image | |
| Import Your Phoenix Scores | Importer vos scores Phoenix | |
| Input Json | Entrée JSON | |
| Is Warmup | Échauffement | `Is` prefix dropped per pt-BR pattern. |
| IsBroken / XXLetterGrade | IsBroken / XXLetterGrade | Untranslated — column headers matching legacy property names (per source comments). |
| Korean Name | Nom coréen | |
| Letter Grade Template / TRUE/FALSE Template | Modèle Rang (lettres) / Modèle TRUE/FALSE | `Modèle` prefix. |
| Levels | Niveaux | |
| Level/Players | Niveau/Joueurs | Combined input label. |
| Location | Lieu | |
| Lock Status / Locked / Unlocked | Statut du verrou / Verrouillé / Déverrouillé | |
| Lock User / Unlock User | Verrouiller l'utilisateur / Déverrouiller l'utilisateur | |
| Machine Name | Nom de la machine | |
| Max / Min (bare) | Max / Min | Capitalized when standalone column header. |
| Maximums / Minimums | Maximums / Minimums | Identical to English (uncommon plural in French but fits the column-header context). |
| Median | Médiane | |
| Minimum Score | Score minimum | |
| Minutes / Seconds | Minutes / Secondes | |
| Missing | Manquant | |
| Monthly Leaderboard | Leaderboard mensuel | |
| Monthly Total | Total mensuel | |
| My Relative Difficulty | Ma difficulté relative | |
| New Player Name | Nom du nouveau joueur | |
| No Recorded Scores | Aucun score enregistré | |
| None | Aucun | Masculine default. |
| Not Relevant to Category | Non pertinent pour la catégorie | |
| Note Count: X | `Nombre de notes : {0}` | |
| Notes | Notes | |
| Original Concept (excel score tracking) Constructed by KyleTT | Concept original (suivi des scores sur Excel) construit par KyleTT | |
| Other | Autre | |
| Overall Letters / Overall Passes | Lettres globales / Pass globaux | |
| Overview | Vue d'ensemble | |
| Parsed Scores | Scores analysés | |
| Percentile Distribution | Distribution par percentile | |
| Permissions | Permissions | |
| Player | Joueur | |
| Player added | Joueur ajouté | |
| Player Levels | Niveaux des joueurs | |
| Player To Test (Must Be Set To Public) | Joueur à tester (doit être configuré en public) | |
| Player Weights | Poids des joueurs | |
| Players (Paste UserId from Account Page if not Public) | Joueurs (Coller l'UserId depuis la page de compte si non Public) | |
| Players synced | Joueurs synchronisés | |
| Points / Points Per Second / Points Pre-Score | Points / Points par seconde / Points pré-score | |
| Potential Conflict | Conflit potentiel | |
| PreBuilt Tournament Configuration | Configuration de tournoi pré-construite | |
| Privacy Policy | Politique de confidentialité | Sentence case (diverges from older `Politique de Confidentialité Complète` Title Case). |
| Priority | Priorité | |
| Private User - X | `Utilisateur privé - {0}` | |
| Reason | Raison | |
| Record Session | Enregistrer la session | |
| Removed | Supprimé | |
| Removed from ToDo List! / Added to ToDo List! | Retiré de la ToDo List ! / Ajouté à la ToDo List ! | |
| Rest Time / Rest Time Per Chart: X | Temps de repos / `Temps de repos par chart : {0}` | |
| Restored | Restauré | |
| Save | Sauvegarder | |
| Save Chart / Save Scores | Sauvegarder la chart / Sauvegarder les Scores | (Save Scores keeps the older Title Case form.) |
| Saved! | Sauvegardé ! | |
| Score: X / Session Score | `Score : {0}` / Score de la session | |
| Search User (Name or UserId) | Rechercher un utilisateur (nom ou UserId) | |
| Seconds of Rest Per Chart | Secondes de repos par chart | |
| See Leaderboards | Voir les Leaderboards | |
| Set Charts | Définir les charts | |
| Show | Montrer | |
| Show Extra Info | Montrer les infos supplémentaires | |
| Show Only Suggested Charts | Montrer seulement les charts suggérés | |
| Show Scoreless | Montrer sans score | |
| Show Top Only | Montrer seulement le haut | |
| Site constructed and maintained by DrMurloc | Site construit et maintenu par DrMurloc | |
| Source / Source Code | Source / Code source | |
| Standard Low / Standard High | Standard bas / Standard haut | |
| Start | Démarrer | Verb sense. |
| Stats | Stats | |
| Step Artist: X | `Step Artist : {0}` | |
| Supported Formats | Formats pris en charge | |
| Target Player Level | Niveau du joueur cible | |
| Test Scores / Test With Player Data | Scores de test / Tester avec les données du joueur | |
| The Category Isn't Interesting to Me' | Cette catégorie ne m'intéresse pas | Source apostrophe-typo not preserved. |
| Title | Titre | |
| TLDR | TLDR | Acronym preserved. |
| Total / Total Charts: X | Total / `Nombre total de charts : {0}` | |
| Total Chart Bonus | Bonus total du chart | |
| Total Popularity Singles vs Doubles / Total Singles vs Doubles | Popularité totale Singles vs Doubles / Total Singles vs Doubles | |
| Tournament Settings / Tournament Name | Paramètres du tournoi / Nom du tournoi | |
| Type | Type | |
| Unknown | Inconnu | |
| Updated X Y | `{0} {1} mise à jour` | Snackbar after a chart save. |
| Upcoming Tournaments / Previous Tournaments / Active Tournaments | Tournois à venir / Tournois précédents / Tournois actifs | |
| User locked / User unlocked | Utilisateur verrouillé / Utilisateur déverrouillé | |
| Verification | Vérification | |
| Video URL | URL vidéo | |
| Video info is not formatted correctly | Les informations vidéo ne sont pas formatées correctement | |
| Welcome / Welcome to Score Tracker, X! | Bienvenue / `Bienvenue sur Score Tracker, {0} !` | |
| Week X - Top Y Charts | `Semaine {0} - Top {1} Charts` | |
| Wouldn't You Like To Know | Vous aimeriez bien le savoir, hein ? | Easter-egg tooltip; uses `vous` despite playful tone. |
| X Brackets | `Tableaux de {0}` | |
| X Charts | `{0} Charts` | |
| X Note counts | `Nombres de notes {0}` | `{0}` = chart type. |
| X Plays | `{0} plays` | Lowercase `plays`. |
| X Progress | `Progression {0}` | |
| X% of Y Comparable Players | `{0}% de {1} joueurs comparables` | |
| Your account is content-locked. … | Votre compte est verrouillé en termes de contenu. … | |
| Your Difficulty Rating | Votre rating de difficulté | |
| Your Score / Your Points / Your Points per Second | Votre score / Vos points / Vos points par seconde | |
| Youtube Hash | Hash YouTube | |

#### Multi-line prose (Life Calculator, ChartScoring, etc.)

The Life Calculator and ChartScoring pages have ~30 dense paragraph entries. They were translated mechanically from the glossary; native-speaker review priority is **high** for these — same caveat as the ja-JP and ko-KR equivalents. Specific candidates:

- All `Life loss description` / `Life gain description` / `Recovery Observations` paragraphs.
- The `Highlighted players have a recorded score on the chart in question.` / `These averages are shifted away from the level in question by .5 of a standard deviation …` algorithm-explainer paragraphs on /Experiments/ChartScoring.
- The `For CoOps, scoring level is simply the lowest level player who's been able to pass the chart.` paragraph and its neighbors on /Experiments/ChartScoring.

Short labels (column headers, button text) are likely fine.

## Process for future batches

1. Pick a feature folder (Tournaments, Tier Lists, Progress, Admin, Tools, etc.) or a category from the Known issues list above.
2. List its English keys (`grep -oP '(?<=L\[")[^"]+' ScoreTracker/ScoreTracker/Pages/<Folder>/**/*.razor` or similar).
3. Cross-reference against `App.fr-FR.resx` to find which are missing.
4. Translate using this glossary. **If a new term needs a decision, add a row to "Established term mappings" before translating.**
5. For the remaining inconsistency fixes (the full Title Case → sentence case sweep, French typographic NBSP normalization, the `Plaque`→`Plate` question if adopted), do **one batch per category** so the diff is reviewable.
6. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.
7. PR titled like `Translate <Folder> to fr-FR` or `Fix fr-FR <inconsistency>`.
