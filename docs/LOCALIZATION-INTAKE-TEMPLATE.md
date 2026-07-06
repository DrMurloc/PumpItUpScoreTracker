# Localization intake template

The baseline for onboarding a **new locale** via a volunteer native speaker. Workflow:

1. Owner asks Claude for an instance: *"give me the intake form for Spain Spanish"* → Claude fills the `{{slots}}` below and hands back a file.
2. The volunteer fills it in (~20–30 minutes).
3. Owner hands the filled form back: *"add this localization"* → Claude generates the full resx, seeds the working glossary, and registers the locale.

Existing locales already have working glossaries (`LOCALIZATION-<locale>.md`); this template is only for bootstrapping a locale that doesn't exist yet.

## For Claude: generating a locale instance

Everything below the horizontal rule is the form. Copy it to a standalone file, fill every `{{slot}}`, and delete nothing else — the `##` headings are a parsing contract for the consume pass; keep them verbatim.

- **§2 Domain terms**: prefill every **AI draft** cell with your best proposal for this language. Cross-reference the existing `LOCALIZATION-*.md` glossaries first — the same term often splits by community (Plate is translated in es-MX/fr-FR but stays English in pt-BR; ja-JP/ko-KR use loanword transliterations). You may append up to **5** extra rows for ambiguities specific to this language; never more, and never delete core rows.
- **§3 Voice & grammar**: adapt each question to the language's real grammar axes (es-ES: tú/usted + vosotros/ustedes; de: du/Sie; ko: 해요체/합니다체; ja: です・ます vs plain, script-mixing). Replace options that don't apply; don't stack every possibility. Five questions is the cap unless the language has a critical axis none of them covers.
- **§4 Example translations**: never prefill. These must be authentically the volunteer's words — they become the style anchor.
- The finished instance must fit ~2 pages. If it's longer, cut yours, not theirs.

## For Claude: consuming a filled form

- Generate `ScoreTracker/ScoreTracker/Resources/App.<locale>.resx` covering **every key in `App.en-US.resx`**. §4 answers go in **verbatim**; §2 and §3 govern everything else.
- A blank or unchanged-draft §2 cell means "translator's choice" — decide, and list the decision under a *native review needed* section in the glossary.
- Seed `docs/LOCALIZATION-<locale>.md` in the same shape as the existing glossaries (style conventions → established term mappings → native review needed → process footer). The intake's content gets folded in; the intake file itself doesn't need committing.
- Register the locale in all three places: `Program.cs` `.AddSupportedCultures(...)`, `CultureController.SupportedCultures`, and the language `MudSelect` in `Components/Account/ProfilePanel.razor`.
- `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.

---

# PIU Score Tracker — {{Language}} localization intake

Thanks for volunteering! This takes **20–30 minutes**. Your answers teach an AI translator to sound like your community across the app's ~700 strings — it can write {{Language}} fine on its own, but it can't know your community's vocabulary or which terms you keep in English.

Ground rules:

- **Not sure? Leave it blank.** Blank means "translator's choice" — never guess.
- **Write `EN` to keep a term in English.** Rhythm-game communities keep a lot of jargon untranslated — don't force a translation nobody uses.
- Your section 4 translations are used **word-for-word** in the app.

## 1. About this locale

- Your name/handle (for credit and follow-up questions):
- The variant you write ({{e.g. "Spain Spanish (es-ES)" — confirm, and note if your answers would differ for other regions}}):
- Where your PIU community talks online (Discord/forum links — optional):

## 2. Domain terms

How does your community actually say these — at the arcade, on Discord? The **AI draft** column is a guess; fix anything wrong in **Your call**. Blank = the draft is fine or you have no strong opinion.

**Playing**

| English | What it means here | AI draft | Your call |
|---|---|---|---|
| chart | one song's steps at one difficulty (never "graph") | {{draft}} | |
| level | numeric difficulty — S21, D24 | {{draft}} | |
| Singles / Doubles | 5-panel / 10-panel modes | {{draft}} | |
| CoOp | multiplayer charts (2–5 players) | {{draft}} | |
| pass | clearing a chart (verb and noun: "no pass yet") | {{draft}} | |
| break / broken | failing out of a chart | {{draft}} | |
| play / run | one attempt at a chart | {{draft}} | |
| session | one outing's worth of plays | {{draft}} | |
| step artist | who designed the chart | {{draft}} | |

**Scoring & judgments**

| English | What it means here | AI draft | Your call |
|---|---|---|---|
| score | | {{draft}} | |
| Perfect / Great / Good / Bad / Miss | the five judgments, incl. plurals ("3 Greats") | {{draft}} | |
| letter grade | SSS+ down to F | {{draft}} | |
| plate | the end-of-song medal — Perfect Game, Rough Game… | {{draft}} | |
| lifebar / life | the health bar | {{draft}} | |
| rating | a computed player rating | {{draft}} | |
| Pumbility | this site's own rating system (proper noun — most locales keep it) | {{draft}} | |

**Content & progression**

| English | What it means here | AI draft | Your call |
|---|---|---|---|
| Score Tracker | this app's name (translate it or keep it?) | {{draft}} | |
| song / artist | titles and artist names themselves never get translated | {{draft}} | |
| Mix | a game version — XX, Phoenix (version names stay as-is) | {{draft}} | |
| folder | a difficulty-level grouping ("the 21s folder") | {{draft}} | |
| skill | a chart trait — runs, drills, twists | {{draft}} | |
| tier list | charts ranked easier→harder within a folder | {{draft}} | |
| title | in-game award title | {{draft}} | |
| bounty | site feature: rewards for scores on neglected charts | {{draft}} | |
| weekly challenge | this site's rotating weekly charts | {{draft}} | |

**Competition**

| English | What it means here | AI draft | Your call |
|---|---|---|---|
| leaderboard | | {{draft}} | |
| ranking(s) | | {{draft}} | |
| tournament | | {{draft}} | |
| qualifiers | | {{draft}} | |
| bracket | knockout stage | {{draft}} | |
| seed | tournament seeding | {{draft}} | |
| stamina | endurance play style | {{draft}} | |

{{Up to 5 extra rows for language-specific ambiguities, or delete this line.}}

## 3. Voice & grammar

Short answers — a phrase or a sentence each.

1. **Formality** — When the app talks to the player ("your scores", "try again"): {{language-specific register options}}? Which register does your community's Discord actually use?
   -
2. **Buttons** — Labels like "Save Scores": {{imperative / infinitive / noun-style — as applicable}}?
   -
3. **Gender** — Where the language forces a choice (greetings, "player"): masculine default, neutral rephrasing, or inclusive forms?
   -
4. **Loanwords** — Overall lean for gaming/tech terms: English loanwords or native translations? (Guides all vocabulary beyond section 2.)
   -
5. **Pitfalls** — What do machine translations of apps always get wrong in {{Language}}? What instantly reads as "translated by a bot"?
   -

## 4. Example translations

Translate these 11 real strings. They ship **exactly as you write them** and set the tone for the other ~700. Keep `{0}` in your translation — it's replaced at runtime.

| # | English | Where it appears | Your translation |
|---|---|---|---|
| 1 | Tier Lists | main menu — the site's most-used page | |
| 2 | Record Session | button that starts recording a play session | |
| 3 | Save Scores | button | |
| 4 | Saved! | toast after saving | |
| 5 | Hide Completed Charts | filter toggle on the chart browser | |
| 6 | Welcome to Score Tracker, {0}! | greeting after first sign-in; {0} = player name | |
| 7 | Invalid username or password | login error | |
| 8 | An account with game tag {0} already exists, is that you? | linking accounts; {0} = a game tag like DRMURLOC | |
| 9 | Your account is not associated with a game profile yet. Play a game on your account first, then try again. | error when importing scores from the official site | |
| 10 | TLDR: Misses matter less at low life, but are always significantly more punishing than Bads. | summary line in the life-calculator explanation | |
| 11 | If you leave this page or cancel, the upload will stop but you will not lose any scores that have already been recorded from your upload. | note on the bulk score-upload page | |

## 5. Anything else

Anything the translator should know that didn't fit above — regional quirks, how your community writes level names, whatever. (Optional.)
