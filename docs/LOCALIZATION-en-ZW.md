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
  `PIU Center`, `Start.GG`, `SkillAttack`, `piuscores`, `DrMurloc`, `YouTube`, `Iolite Sky`, `BITE`,
  `Murloc`. A Murloc still has to be able to find the login page.

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
