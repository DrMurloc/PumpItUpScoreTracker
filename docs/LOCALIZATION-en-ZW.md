# en-ZW (Murloc) localization glossary

Working reference for translating `App.en-US.resx` into `App.en-ZW.resx`. Murloc is the site's
joke locale — it is not a human language and has no volunteer translator, so unlike every other
locale doc here, this one is a **specification** rather than a record of native-speaker judgment.

For the localization mechanism itself (resx layout, `L["..."]` usage, key conventions), see
[ARCHITECTURE.md](ARCHITECTURE.md). For PIU domain terms in English, see [DOMAIN.md](DOMAIN.md).

> **Status (2026-07-28):** Live, at full key parity with en-US (it used to be deliberately
> partial). The alphabet rule below is enforced by `LocalizationKeyTests.MurlocValuesUseOnlyTheMurlocAlphabet`.

## Why this document exists

The locale was created 2023-10-27 (`c4dbb61f`) with 139 hand-written entries. On 2026-04-25
(`57dce2ba`) an expansion pass **overwrote all 139** and grew the file to ~1,700 — and every batch
after that invented its own idea of what Murloc sounds like. By 2026-07-28 the file held at least
five mutually inconsistent dialects: pure gurgle, vowel-mangled English (`Srglrch` for "Search",
`Blrrgk` for "Back"), a WoW-flavoured register (`Unggh`, `Nk`, `Shhhcret`), English words left
verbatim inside gurgle sentences, and 297 values that were **untouched English**. 1,419 of 2,188
entries failed a no-English test.

A joke locale that renders half its strings in English is not a joke, it is a bug that looks
deliberate. The rules below exist so the next batch cannot re-open that hole, and the arch test
exists because a rule with no ratchet is what produced the five dialects.

## Style conventions

- **The alphabet is `a b g l m o p r u`, and nothing else.** Every letter in every value must come
  from that set. This is derived from the 2023 hand-written entries, which used `g l m o p r u`
  almost exclusively, plus `b` and `a` (owner ruling, 2026-07-28) so words like `Blarg` and `Blub`
  are available. A value containing `c`, `d`, `e`, `f`, `h`, `i`, `k`, `n`, `s`, `t`, `v`, `w`,
  `x`, `y` or `z` is a bug — those letters are how English leaks back in.
- **Never transliterate English.** Mangling vowels out of an English word (`Search` → `Srglrch`,
  `Off` → `Mrgloff`, `account` → `Mrglccount`) is the single most common past failure. It reads as
  English with a speech impediment, not as another language. The word must be built from Murloc
  syllables and carry no trace of its English source.
- **One English word maps to one Murloc word, everywhere.** The 2023 file called a chart `Murgl`
  in one entry and `mrgl` in another; the 2026 file used `Mrrglrgrl`, `Plglrglgrrr` *and*
  `Mrglrgl`. Pick from the table below, or coin once and reuse forever.
- **Match the English word count and capitalization.** Title Case in, Title Case out; a five-word
  English value becomes five Murloc words. Murloc words are typically shorter than their English
  counterparts — do not pad.
- **Intensity rides on repeated `r`.** This is the owner's own device: `Very Easy` → `Mrgl Mrrrrrgl`,
  `Very Hard` → `Mrgl Grrrrrrrrrrmgrl`. Comparatives escalate the same way (`mrgl` → `mrrgl` →
  `mrrrgl`) rather than borrowing the English `-er` suffix, which would need a letter outside the
  alphabet.
- **Preserve placeholders verbatim.** `{0}`, `{1}`, `{0:N0}` go through untouched and in the same
  set as the English value.
- **Preserve punctuation, digits and symbols.** `—`, `·`, `▲`, `▼`, `#`, `%`, `!`, `?` all survive
  exactly as the English has them. Only letters change.
- **Acronyms and brand names stay English.** Any all-caps token of two or more characters is left
  alone (`BPM`, `NPS`, `CSV`, `URL`, `MB`, `SSS`, `AA`, `PG`, `MG`, `UG`, `API`, `PUMBILITY`), as
  are the protected proper nouns: `Pump It Up`, `Phoenix`, `Discord`, `PIUGame.com`, `piugame.com`,
  `PIU Center`, `piucenter`, `Start.GG`, `SkillAttack`, `piuscores`, `DrMurloc`, `YouTube`,
  `Iolite Sky`, `BITE`, `Murloc`. A Murloc still has to be able to find the login page.

## Syllable inventory

Coin new words by concatenating one to four of these. They are the syllables observable in the
2023 originals plus the `a`/`b` forms the 2026-07-28 ruling permits.

```
mrgl  mrg   mr    murgl murg  mur   murp  morp  morg  mo
gl    glr   grgl  grg   gro   grog  rgl   rorg  ro    rorgl
blub  blurg blarg brgl  blgrl glub  glorg glarg
argl  arg   urg   ur    plgl  prgl  lurg  lrgl  mrp
marg  magl  gurg  murm  mrrgl grrgl murrgl grorp mrogl
```

Word length tracks the English: ≤3 letters → one syllable, 4–6 → two, 7–10 → three, 11+ → four.

## Established term mappings

Recovered from the owner's 2023 hand-written entries at `c4dbb61f` and re-seeded across the whole
file on 2026-07-28. **Reuse these; do not re-coin them.**

### App / generic UI

| English | en-ZW | Notes |
|---|---|---|
| About | Rrglrmrgl | |
| Account | Grgl | Plural `Grglrgl`. |
| Actions | Grlgmrg | |
| Average | Rorg | |
| Broken | Morgl | |
| Cancel | Margl | |
| Close | Grorp | |
| Completed | Morgrorp | |
| Favorites | Gromorg | |
| Language | Mrglrglrglrglrglrglrgl | Deliberately absurd length — it is the language picker. Keep it. |
| List | grog | |
| Login | Mrglmrp | |
| Logout | Mrglmrg | |
| Name | Mrg | |
| Preference | Mrglrgl | |
| Progress | Mrglrlgl | |
| Public | Mrglrgll | |
| Restart | Mrglrlgrlgl | |
| Tool / Tools | Rorgl | One word for both — Murloc does not mark number here. |
| Type | Morg | |
| Username | Grrmrrgl | |
| Video | Grrrgl | |
| Hour / Hours | grogl | Coined 2026-08-03 for the console's `· 24 grogl` stat labels. |
| GameTag | Grglmrg | `Grgl` (account) + `Mrg` (name). |
| Code / Source | Murgblub | Already carried "Source & contact" (`Murgblub opa golba`); the Code tab reuses it. |
| Publishes | Grolub | Coined 2026-08-06 for the sharing copy, which names publishing source as the gate. Not `Murpro` — that is already "Shared". |
| Insights / Activity | Murgblarg | The console tab and the "recent activity" strings share one word. |

### PIU domain

| English | en-ZW | Notes |
|---|---|---|
| Chart / Charts | Murgl | The single most-reused noun in the file. |
| CoOp | Mrglrl | |
| Doubles | Groggl | |
| Grade | Rorgl | |
| Leaderboard | Mrglrmrgl | Plural `Mrglrmrglrgl`. |
| Mix | Mrg | Plural `Mrgrgl`. |
| Plate | Murp | |
| Player / Players | Morp | |
| Randomizer | Rmrgmrml | |
| Rating | Mrglrg | |
| Score | Mrglrgl | Plural `Mgrlgmrg`. |
| Singles | Mrglrlgl | |
| Song | Mrgl | |
| Title / Titles | Mrgl | |
| Tournament(s) | Mrllrlrlrlgl | |
| Download | Murlg | |
| Rival / Rivals | Murgmorp | `murg` + `morp` (player) — the player you chase. Number unmarked, like Tool/Tools. Coined 2026-08-04 for the Rivals feature. |
| Block | Murgloo | Recovered from the existing "Block this tool"; reused across the rivals blocking strings. |
| Blocked | Murgloogro | |
| Unblock | Momurgloo | `mo` (negation, as in the existing "Mrp"/"Bo" negatives) + `Murgloo`. |
| Crew | Grogblub | A community you are in, said casually — distinct from `Plglmurgblub` (Community). |
| Official | Urgmrmurg | Title-case form of the existing lowercase `urgmrmurg`. |
| Feed | Glorgmorg | Recovered from the existing "Feeds" entry. |
| Board | Mrglblarg | Plural `Grrglplgl`. Distinct from `Mrglrmrgl` (Leaderboard). |
| Shared | Murpro | Recovered from the existing "One shared board" entry. |
| Supplement / Supplemented | Blurgmorglmrgl | Coined 2026-08-04 for the supplemented leaderboard reading. |
| Roll up | Mrgloru | Coined 2026-08-04 — the weekly gather, not a rolling motion. |
| Snapshot | Barglub | Coined 2026-08-04. |
| Data | Gru | Coined 2026-08-04. |
| From | Blarg | Coined 2026-08-04. |
| Find | Grubmarg | Coined 2026-08-04. |
| Row / Rows | Grolgub | Coined 2026-08-04. |
| Edge | Bralmo | Coined 2026-08-04 for the supplemented-row legend. |
| Gain | Roglub | Coined 2026-08-05 for the digest's PUMBILITY gain. The amount won, distinct from `Romr` (gainer), the player who won it. |
| Across | Plglarg | Coined 2026-08-05 for the digest's board-climber line. |
| On | Ur | Coined 2026-08-05 — the preposition, as in "50× AAA on singles". |


### Difficulty and intensity

| English | en-ZW | Notes |
|---|---|---|
| Easy | Mrgl | |
| Easier | Mrrgl | One extra `r`. |
| Easiest | Mrrrgl | Two extra. |
| Hard | Mrglmrgl | |
| Harder | Mrrglmrgl | |
| Hardest | Mrrrglmrgl | |
| Medium | Mmmmrgl | |
| Very | Mrgl | Intensity is carried by the following word's `r` count, not by this word. |
| More / Most | Mrrgl / Mrrrgl | |
| Less / Least | Mgl / Mggl | |

### Function words

English function words have no Murloc equivalent to recover, but the word-count rule means a
seven-word English sentence needs seven Murloc words — so they get coined once and reused rather
than dropped. Kept deliberately short, since the length rule tracks the English.

| English | en-ZW | Notes |
|---|---|---|
| a | a | |
| for | ba | |
| on | ap | |
| to | ro | Not `mo`, which is the negation prefix (`Momurgloo`). |
| one | mrp | The pronoun ("similar to this **one**"), not the numeral. |
| yet | larg | Also carries "still", as in *still isn't enough*. |
| Manual | Blargurg | Coined 2026-08-05 for the score dialog's manual edit heading. |
| Similar | Plglrogrgl | Distinct from `plglro` ("like"), which the *Charts like this* heading already uses. |
| at | og | Coined 2026-08-07 for the title drawer's per-grade rows ("S13 **at** SSS+"). Distinct from `ap`/`ur`, both "on". |
| or | ol | Coined 2026-08-07. |
| not / isn't | bo | Recovered from the file's existing `Bo` negatives; tabulated so the next batch reuses it rather than re-coining. |
| enough | blurg | Coined 2026-08-07. |
| fifty | murgro | Coined 2026-08-07 for *Fifty charts, {0} plate.* — the numeral spelled out, where `mrp` ("one") is the pronoun. Digits stay digits; only a spelled-out English numeral needs this. |

### The PUMBILITY calculator batch (2026-08-16)

Coined for `/PumbilityCalculator/{mix}` ([pumbility-calculator.md](design/pumbility-calculator.md)) and
reusable everywhere. Its formula vocabulary is the part most likely to recur.

| English | en-ZW | Notes |
|---|---|---|
| base | gogl | The formula's `Base(level)`. Also the verb — *priced / pays / costs* all read `gogl`, since Murloc does not mark the difference. |
| formula | blargmurm | |
| multiplier / modifier | murmgrog | |
| bonus | roglubgl | `roglub` (gain) + `gl`. |
| floor (a grade's) | blogl | Distinct from `grogl` (ceiling). |
| ladder | gropla | A rung of it is `grop`, which also carries *step*. |
| exchange | blorg | The exchange rate is `blorg mrglrg` — `mrglrg` (rating) doubles as *rate*. |
| worth | gromb | The adjective. The noun *value* stays `grumb`. |
| buys / bought | blogub | What scoring buys, in levels. |
| push / pushing | gruplo | *Push levels* — the imperative the answer section leads with. |
| band | grolgub | Shares the word with *row*, which is what a band is on the page. |
| population | morpgro | `morp` (player) + `gro`. |
| sweep (the nightly job) | grrgro | |
| axis | plagub | |
| curve | grulub | Quadratic doubles it: `grulubgrulub`. |
| span / gap | grulm | |
| tiebreaker | grulmplub | |
| magnified | grrglgro | |
| lever | graplub | |
| ⚠ a lone grade letter | Ｓ Ｄ Ｃ Ｆ | **Fullwidth**, when the letter stands alone as game notation (*the S grades*, *a D pays*, *S + D*). A bare Latin `S` or `D` is a one-letter English word to the alphabet ratchet, and a Murloc syllable in its place would stop naming the grade. Grade names of two or more characters (`AA`, `SSS+`) are already exempt as acronyms. |

## Process for future batches

1. A new en-US key gets an en-ZW value **in the same pass**, like every other locale.
2. Reuse a term from the tables above if one applies; otherwise coin from the syllable inventory
   and add the new word to this doc.
3. Run `dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj` — the alphabet
   ratchet fails the build on any letter outside `a b g l m o p r u`, so a mangled-English value
   cannot merge.

## Known issues

- `LIVE` and `LAMP` are English words that survive as all-caps tokens under the acronym rule.
  Both are domain shorthand and read as a shout in any language; left deliberately.
- The 2023 originals contained three joke shout-outs to real people (`MR_WEQ`, `daryen`) and left
  the word `votes` in English in two entries. Those keys have since been deleted as orphans, so
  the jokes are gone. If they are wanted back, they need new homes rather than restored keys.

## PUMBILITY peers (Phoenix 2 projection, 2026-08-16)

| English | en-ZW | Notes |
|---|---|---|
| Peer / Peers | maglmurp | Recovered from the existing `#{0} of {1} peers` (`maglmurp`); `PUMBILITY peers` is `PUMBILITY maglmurp`. Distinct from `Murgmorp` (rival) and `Morp` (player). |
| Pool | blubgro | Recovered from the existing `Pool` → `Blubgro`; lowercase mid-sentence. |
| within | arglblgrl | Recovered from `within 1 level` → `arglblgrl 1 grorpmurm`. |
| full | mrglmr | Recovered from `Full board` → `Mrglmr mrglblarg`. |
| projection / projections | purgmorgl | Recovered from `{0} have no projection` → `purgmorgl`. |
| show / shown | romorg / blgrlargl | `Romorg` from `Show fewer`; `blgrlargl` (shown) from the score-history line. |
| than | grogrgl | Recovered from `More than 75%` → `Mrrgl grogrgl 75%`. |
| them / their | grglblarg | Recovered from `kept their level` → `grglblarg grorpmurm`. |
| stand (on) | morggrrgl ap | `morggrrgl` from `Where you stand`; `ap` is the existing "on". |
| Peers IQR | Maglmurp IQR | The `/Pumbility` targets' column. `IQR` is an acronym and rides through as one. |
| From {0} peers | Romurg {0} maglmurp | `Romurg` from `From {0} scores` → `Romurg {0} mgrlgmrg`. |

## Stage breaks and max combo (2026-08-17)

Coined for [stage-breaks-and-max-combo.md](design/stage-breaks-and-max-combo.md) — the session
row, the chart journey and the admin backfill.

| English | en-ZW | Notes |
|---|---|---|
| Stage break | Mrpmurg arglglorg | `Mrpmurg` (stage) + `arglglorg` (break), both recovered — the compound is what a Murloc calls a song that stopped. Distinct from `Morgl` (broken), which is a run that failed and finished. |
| in (how far through) | gropmur | Recovered from `On charts you've never passed` → `Lurg murgl **gropmur**` — the "through/into" sense, not the preposition `ap`/`ur` (on) or `og` (at). `31% gropmur` is 31% of the way in. |
| max (superlative) | gro | Recovered from `Max combo` → `Gro grogrgl`. Reused for `Backfill max combos` → `Mrglrglmrp gro grogrgl`. |
| re-solve / recalculate | mrgrglmurm | Coined for the backfill's snackbar; `mrg` (again) + `rglmurm`. |
| judged | blgrlmurgro | Coined 2026-08-17 — a score the game handed a breakdown for. |
| note (a step) | roblub | Recovered from the life-calculator's `clean notes` → `plglmurm **roblub**`. |
| history | lurgmrmorg | Recovered from the existing `History` entry. |

## The peers page batch (2026-08-18)

| English | en-ZW | Notes |
|---|---|---|
| Prevalence | Murgroblarg | Coined for the Phoenix 2 Play page's grouping — how much of the peers' pools a chart accounts for. |
| Variability | Blgrlmurm | `blgrl` (split) + `murm`. The five levels build off it: `Mrgl Grrrogmurm` (very consistent), `Grogmurm` (consistent), `Argmurg` (mixed), `Blgrl` (split), `Mrgl Blgrrrl` (very split) — intensity rides the repeated `r`, the owner's own device. |
| repriced | murgroblub | Coined for the carried Phoenix 1 line. |
| weighted sum | murmgro grubgro | `murm` (weight, from Weekly's `Murmmr`) + `gro`; `grubgro` is the sum. |
| overlap / in common | grogblub | Coined for the roster's column and the compare strip's tile. |
| and | ap | Recovered from the existing "on" — Murloc does not distinguish them. |
| here | ap | Same word; position is carried by word order. |
| there | gro | Coined alongside it for the carried line. |
| most | mrrgl | Recovered from `More than` → `Mrrgl grogrgl`. |
| below / under | glub | Coined for the roster's window and the waiting room, opposite `mrrgl` (above/most). |
| worth / value | grumb | Recovered from `Value` → `Grumb`. |
| target | murgroblub | Reused from `repriced` — a Murloc names the thing by what it does to the number. `PUMBILITY Targets` is `PUMBILITY Murgroblub`. |
| Highest / Lowest | Mrrglmrrgl / Glubglub | The doubled root is the superlative, the owner's own repetition device: `mrrgl` (most/above) and `glub` (below). `Mrgl Mrrgl` is very high; `Mrrgl` alone is high. |
| only | ub | Recovered from `#{0} of {1}` → `#{0} ub {1}` — the same particle carries "of" and "only". |
