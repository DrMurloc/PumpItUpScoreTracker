# Phase 2 - Mocks

**Runs** only on the engineer's yes to product scope's suggestion - a new page, a UI change, an
entirely new MCP tool, or a Slack message or interaction whose wording needs vetting
(steps/1-product-scope.md, "Moving on"). It is never forced. **Decides** what the agreed needs look
like. **Stays on** the picture: the needs are settled, so a mock that turns up a new need raises it
as a question rather than quietly deciding it.

## Building them

- **Make it look good and feel intuitive.** Much of Astrid shows what AI can build, and a mock is
  where that shows first. Take it as far as you can: to the limit of what you can build, not just
  far enough to answer the question.
- **A UI the ticket references but doesn't define** -> one plain HTML artifact in the side panel:
  the real Astrid shell and page on Astrid's tokens, working interactions, a small controls bar for
  variants, and a "show what changed" mark when it changes an existing surface. This is the scoping
  mock: for the person scoping the ticket to try out and change, and once approved, the agreed
  picture of what the feature is. Never the Design canvas artifact type. Never committed.
- **An entirely new MCP tool** -> its name, description, parameters and a sample JSON response,
  inline. The question to ask of it: would an agent read it well?
- **A Slack message or interaction from Astrid** -> its text and controls as they would post, inline,
  so the words a person reads there can be vetted.
- **Fields are a list, not a picture.** When the surface a mock shows carries new fields, the mock
  defines them as a list - each field's name, what it holds, its values - beside the picture.
- Revise in place - v2, v3 at the same link - as feedback comes in.
- **Name things in words people already use.** A mock is no place for a word to be born: mark any
  new term as new, so nobody mistakes it for one already in use.
- **Sharing is only for when someone explicitly asks for a mock** - never a step of the loop, and
  never a question it raises. An artifact is private to the person who owns it, and its link goes on
  the ticket as it is. When someone asks, its owner shares it from the page's Share menu, or, for
  anyone beyond the team, a stakeholder mock is made (below).
- **A mock for anyone beyond the team** (a department, stakeholders, another team) is a stakeholder
  mock, made separately when someone explicitly asks for one, often a cut-down copy of the scoping
  mock. It meets the bar in `docs/ARCHITECTURE-UI.md` section 1: it shows, it doesn't tell, and if
  it needs explaining, the mock is what gets fixed.
  - Nothing inside it that the real screen wouldn't show: no cover, legend, notes or explanation
    boxes. Context and disclaimers (what's sample, what's real and as of when) go above or below it.
    A behavior the picture can't show goes in the ticket.
  - No new word: the scoping mock's new-term marks don't carry over, so say it in words already in
    use.
  - One file per audience, holding only what that audience may see.
  - No sales copy, and no position the engineer hasn't taken (cost, policy, another team's work):
    the scenes make the case.
  - Every page looked at rendered, at the size its reader will see it, before it goes out, or the
    checkpoint says it couldn't be.

## The message

The frame from SKILL.md, headed "Phase 2 of 8: Mocks". Body: the link and what to look at first;
what changed since the last version; the new terms the mock marks; any need the mock turned up.

**What I need from you:** the mock review, by link; each new term; each question the mock raised,
with a recommendation.

## Moving on

When you think the mock is approved - no question open - the checkpoint asks one question: "Post the
mock on the ticket and move on to <next phase>?" The comment is headed **Mocks - approved by
<engineer> on <date>**: each mock's link and what it shows.
