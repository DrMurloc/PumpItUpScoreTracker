# Community-text translation

Exploratory. The chart-comments feature is not being built; this is the localization half of it,
measured on real data so the design question is settled before anything ships.

**The question**: community text arrives in whatever language the player writes in, and has to
reach readers in five. What does that cost, and does it need the most expensive model?

**The answer**: about **$11–22 a month at a thousand comments**, and **Sonnet 5 does the job**.
Haiku 4.5 does not, in a way that is worth reading about below.

---

## 1. Pivot through English

Two calls per comment, not one:

```
original (any language)  →  English + register metadata      (stage 1, "pivot")
English + register       →  es-ES, fr-FR, ko-KR, pt-BR       (stage 2, "fan-out")
```

Fanning out directly from the source would be one call instead of two, and marginally cheaper.
It was rejected because of what it does to the glossary. Direct translation needs a
**many-to-many** term table — Korean 채보 has to reach Spanish as `chart`, Portuguese as `chart`,
French as `Chart` — maintained across nine locale documents and re-derived every time a volunteer
changes a term. Pivoting needs **one hop per locale**, which is exactly the shape
`docs/LOCALIZATION-<locale>.md` is already written in.

Three things fall out of the pivot for free:

- **A sixth locale is a stage-two rerun.** The English is stored, so nothing re-reads the original.
- **A canonical English form** exists for moderation, search, and Discord cards.
- **Cost scales with comments posted**, not comments read.

### The problem pivoting creates, and the fix

English cannot encode a Korean speech level or a tú/usted choice. A naive pivot flattens the
author's register on the way through — the one thing worth preserving.

So stage one emits register as **fields**, not prose:

```json
{ "source_language": "ko", "english": "...", "register": "polite",
  "formality_marked": true, "tone": "sarcastic criticism", "entities": [...] }
```

Making the reading explicit is arguably *more* faithful than direct translation, where the same
nuance can quietly evaporate with nothing to point at. `formality_marked` distinguishes "the
author chose polite" from "the language never offered a choice" — the second is where a house
default applies rather than a mirror.

### The rule stage two turns on

- **Dialect belongs to the target.** es-ES output uses *vosotros*, even when the source was
  Mexican. Otherwise it reads foreign to the person it is for.
- **Register belongs to the author.** Blunt stays blunt, sarcastic stays sarcastic.
- **Where the source encoded no formality**, neutral-polite: 해요체, tú, tu, você.

---

## 2. The glossary contains only what a model cannot know

This is the load-bearing methodological decision and it changed the result.

A model already knows that Korean ㅋㅋㅋ is *jajaja* in Spanish and *kkkkk* in Brazilian
Portuguese, that *no manches* is casual Mexican, and how Korean speech levels work. Putting any of
that in the prompt costs tokens for nothing — **and destroys the measurement**, because whatever
the prompt supplies, every tier gets right. Teach it, and the cheap model stops being
distinguishable from the expensive one exactly where it matters.

So the split is:

| In the prompt (unknowable) | In the evaluation (should already know) |
|---|---|
| `피펨즈` = Fefemz | `ㅋㅋㅋㅋ` → jajaja / kkkkk / mdrrr |
| `Big One` = `B1G` | Korean speech levels render correctly |
| `Mix` → `시리즈` (this codebase's call over `믹스`) | tú/usted, tu/vous conventions |
| `chart` → EN in es-ES, `채보` in ko-KR | sarcasm inverts (`대단하네요` ≠ "that's amazing") |
| `D29` — never translated, never reformatted | emoji, timestamps, line breaks survive |

The authored glossary is ~30 rows. The song and player tables are hand-seeded from the corpus for
now; the site already stores per-culture song names (`SongCultureName`) and game tags, so
**generating them is the next step, not another authoring job**.

---

## 3. Results

23 real YouTube comments (`ExplorationTests/Translations/TranslationCorpus.cs`) — English, Korean,
Spanish, one Portuguese — into four locales, three arms, thinking disabled, synchronous.

| Arm | Per comment | Per 1,000/mo | Batched | Entities kept | Language detected | Failures |
|---|---|---|---|---|---|---|
| Opus 5 | $0.0363 | $36.34 | $18.17 | **89/89** | 23/23 | 0 |
| Sonnet 5 | $0.0218 | $21.77 | $10.88 | **89/89** | 23/23 | 0 |
| Haiku 4.5 | $0.0056 | $5.59 | $2.79 | 81/89 (91%) | 23/23 | 0 |

Costs ran ~2.5× the pre-run estimate. The prompts are ~2,400 tokens per call and the system prompt
is sent twice per comment, so input is now ~60% of the bill rather than the ~35% assumed. **Prompt
caching does not help at this volume** — a thousand comments a month is 1.4 an hour against a
five-minute cache TTL, so the cache is written and never read. Splitting the glossary so each
stage carries only the half it needs would cut input ~30% and is the obvious optimization if
volume ever justifies one.

### Where Haiku fails, and why it matters

Haiku's numeric miss is narrow — it dropped `1000%` and `💯` from two comments. The real failure
is the one the deterministic checks cannot see, and it is exactly the thing the prompt
deliberately did not teach.

Given a Korean comment ending `ㅋㅋㅋㅋㅋㅋㅋㅋ`:

- **Opus 5** → `jajajajajaja` (es), `mdrrrrrr` (fr), `kkkkkkkkkk` (pt), `ㅋㅋㅋㅋㅋ` (ko).
  Frog noises localized too: `croac croac`, `croa croa`, `croc croc`, `개굴개굴`.
- **Sonnet 5** → `jajajajaja`, `mdrrrrrr`, `kkkkkkkk`. One slip: French frogs became `coin coin`,
  which is a duck.
- **Haiku 4.5** → `ㄱㄱㄱㄱㄱㄱㄱ` — the wrong Korean letter, emitted **into the Spanish, French and
  Portuguese output**. It also left `braindead` and `attention-whore` untranslated across all three.

Had the prompt listed the laughter conventions, all three arms would have passed and the sweep
would have concluded that Haiku is fine at a fifth of the cost. Not teaching it is what made the
comparison mean anything.

### What every tier got right

- **Sarcasm survives the round trip.** `참 대단하네요` → *"is really something"* → back to
  `참 대단하네요` in ko-KR, `es de verdad algo digno de ver` in es-ES, `c'est vraiment quelque chose`
  in fr-FR. All three arms.
- **Dialect held.** Mexican `ustedes` → peninsular **vosotros** in es-ES, on all three.
- **Glossary held.** `chart` in es-ES/pt-BR, `la Chart` (feminine) in fr-FR, `채보` in ko-KR.
- **Structure survived.** The meme comment kept its `2:01`, its blank line, and its quoted
  punchline everywhere.
- **Source language was detected 23/23 by every arm**, including an English comment posted from a
  Korean-handle account.

---

## 4. Where it stands

**Recommendation: Sonnet 5, batched — ~$11/month at a thousand comments.** It matched Opus on
every mechanical measure and lost only on idiom polish. Opus is available for ~$7/month more if a
native reader finds Sonnet's output thin.

**Not settled, and deliberately so:**

- **A native pass on ko-KR.** Neither the owner nor Claude can grade Korean output. The es-ES and
  pt-BR renderings are spot-checkable; Korean needs a volunteer before anything ships.
- **es-MX.** Left out: peninsular and Mexican Spanish are mutually intelligible, the es-MX
  catalogue has known contamination, and the original is always displayed alongside. The stored
  English pivot makes adding it a stage-two backfill.
- **Moderation.** Deliberately dropped. The corpus settles why: its three heated comments —
  sarcastic criticism of chart vetting, `관종`/`개-` intensifiers about a chart, and `pelotudos`
  about crowd noise — are all community heat about *charts*, none about anyone's identity. A layer
  tuned to over-flag would have caught all three and taught the owner to ignore it. If it is ever
  added: it must read the **original**, never the English pivot, because translation launders the
  thing being detected.
- **Storage.** The pivot would be stored (owner's call) but not displayed — useful for debugging
  and for the locale-backfill path. No schema exists; that lands with the comments feature.
- **The 500-character cap**, raised from 200 after four of 23 real comments exceeded it. The
  longest are the heartfelt replies; a 200 cap would have truncated those and kept `FATALITY`.

## 5. Running it

Every test bills a real account. `ClaudeApi:ApiKey` in the AppHost user-secrets store; inert
without it. See [HOW-TO-TEST.md](../HOW-TO-TEST.md#translation-workbench--spends-real-money-manual-runs-only).
