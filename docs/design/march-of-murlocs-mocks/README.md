# March of Murlocs — Slice 1 mocks

Signed off 2026-08-11. These are the design artifacts behind
[§11 of the plan](../march-of-murlocs.md#11-the-surfaces) — the doc is the specification and
these are what it was agreed against. Open any file directly in a browser; they are standalone
HTML with no build step, no network calls and no dependencies.

| | Surface | Route |
|---|---|---|
| [1](1-season.html) | **Season** — the landing page | `/MarchOfMurlocs` |
| [2](2-session-breakdown.html) | **Session Breakdown** | `/MarchOfMurlocs/Session/{id}` |
| [3](3-submit.html) | **Submit** | `/MarchOfMurlocs/Session/{id}/Edit` |
| [4](4-planner.html) | **Planner** | `/MarchOfMurlocs/Planner` |
| [5](5-discord-card.html) | **Discord card** | — |
| [6](6-past-seasons.html) | **Past seasons** — a dialog, no route | — |

They are interactive, and the interactions are the point: flip densities, drag the Planner's rest
slider, run the Submit page's import, switch the Season page between mid-season and opening week.
Each carries a **mock bar** across the top naming itself and saying what its data is.

## The data is real

**Every figure came out of a production-synced database**, not from imagination — 김재현's Winter
2025 Doubles session and his 129-chart Phoenix Doubles record book, the real Winter 2025 boards,
DrMurloc's actual rivals and community memberships, and the two seasons' frozen scoring
configurations. Everyone shown has a public profile, and MoM sessions are public on the board by
rule regardless.

That is why the mocks kept finding things. Two live defects (§2.8, §2.9) and one persistence
trap (§9.5b) came out of drawing pages against real numbers; none of them would have surfaced
against invented ones. **Do the same for the next slice's mocks.**

Where something is *not* real it is called out in the page's own footnote. There are three, all
because no real example exists yet: tieny's second Doubles session on the Season page (multiple
sessions per season postdate all data, D16), the live Winter 2026 season in the Past seasons
dialog, and the Submit page's draft.

## What they cannot show

- **Jacket and avatar art is external** and the Artifact sandbox blocks it, so those boxes are
  placeholders at the real pixel budget. On the site the jacket is the identifier (UX rule 2).
- **Discord custom emoji** are drawn as CSS chips. The real card ships `#DIFFICULTY|…#`,
  `#LETTERGRADE|…#`, `#PLATE|…#` and `#MIX|…#` tokens and the adapter swaps them.
- **MudBlazor** is not loaded. Controls are hand-built to match the components they stand in for —
  the density buttons mirror `/Pumbility`'s `MudIconButton` group, the chart-type group mirrors the
  tier lists' `MudButtonGroup`.

Colours are the Phoenix palette lifted verbatim from `Services/Theming/MixThemes.cs`, and the row
highlight set (`.is-me` / `.is-rival` / `.is-community` / `.is-both`) is copied from `site.css`
rather than re-derived, so a MoM board wears the sitewide standard.
