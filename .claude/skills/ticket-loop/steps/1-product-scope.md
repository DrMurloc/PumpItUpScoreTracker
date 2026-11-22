# Phase 1 - Product scope

**Decides** what we are building: who it is for, what changes for them, and what the data shows.
Product scoping is defining the needs. **Stays on** the needs: nothing here shows what the feature
looks like - no mock, no sample tool response, no drafted copy - and nothing says how it gets built -
no layers, files, schema or tests. A technical unknown stays out rather than guessed at: it is named
by its kind (Unknowns, below), never answered here.

It runs one of two ways:
- **The full pass** - there is no ticket yet, the ticket is a stub or napkin (labeled so in its title
  or description, or plainly just a concept outline), or its definition has gaps. When the engineer
  says "workshop", expect several rounds of findings, forks and questions before the needs settle.
- **Understanding the ask** - the ticket is already well scoped, or earlier phases are finished on it
  (SKILL.md, "Load"). One short checkpoint: what the ticket asks and for whom, what is clear and what
  isn't, and, when earlier phases are finished, what changed on main since and what that changes.
  Anything unclear is a question; once nothing is, ask to move on.

## Research

- **Today:** how the feature works now, in the words its users would use. Read only as much code or
  UI as that takes.
- **Data exploration loops:** size the problem and check the product assumptions against real data -
  how many people, rows or tests it touches, what the edge cases look like. Cheapest first: the
  Astrid MCP's stats and list tools, a local database from the production bacpac
  (`docs/HOW-TO-RUN.md`), reads on the QA database server. A production database read or an exploration test
  needs a yes: ask it as a question.
- **Feedback loops:** who else has to answer something - someone to consult: name them and the
  question.
- **No ticket yet:** find the epic it will likely go under (SKILL.md, "Jira"). The first checkpoint
  names it, because dark or live - and so whether anything can be deferred - follows from it.

## Unknowns

Sort each unknown the needs leave by its kind (SKILL.md, "Unknowns, by kind"):
- **A product requirement at the ticket's core** is a feature question now, and until it is answered
  the ticket isn't Product Scoped, whatever else is settled *(explicitly defined by engineer: "if
  there's full blown undecided stuff those should not be product scoped")*. A mock often answers
  it. A detail the spec can settle doesn't count.
- **An open or closed loop** is a step of its own in the Ask, marked as one - "**Engineering (open
  loop), first:** ..." - and the loops left set the title's tag (SKILL.md, "Jira").
- **An outside input** is written so the code merges without it: the Ask names who holds it, and no
  criterion waits on it.
- **A technical decision** is named, never answered here.

## The message

The frame from SKILL.md, headed "Phase 1 of 8: Product scope". Body sections in this order, each only
when it has content:

1. **Scope verdict** - one line: well scoped (understanding the ask), scoped with gaps, or napkin
   (the full pass).
2. **How it works today** - two to four sentences.
3. **What changes, for whom** - the ask and the deliverables as people and agents will experience
   them - pages, MCP tools, Slack posts, jobs - described in words. Not drafted, and not files or
   classes.
4. **What the data showed** - each check you ran and what it found.
5. **What's still unknown** - each unknown (Unknowns, above) with its kind, and the title tag they
   add up to. A product requirement points at its question.
6. **What I'm assuming** - each assumption and how it gets settled: "nothing needed from you", or
   the question that settles it.
7. **For later** - out of scope, follow-ups, deferred questions. Needs nothing from the reader.

**What I need from you:** the feature questions the ticket leaves open, each with a recommendation
(on a dark epic, each also offers Defer); any data read that needs a yes; anyone to consult.

## Moving on

When you think the needs are settled - no question open - the checkpoint recaps what they are and
asks one question, which moves on. **The ticket body is the product scope:** nothing posted restates
it, and a comment records only where the phase departed from it. *(explicitly defined by engineer)*
- **No ticket yet:** "File the story under <epic> and move on to <next phase>?" The story carries what
  this phase settled under Decisions locked, each entry provenance-tagged (SKILL.md, "Jira", for
  where it goes and its body).
- **The phase departed from the body** - a criterion dropped or changed, a decision the body doesn't
  hold, a fact in it corrected: "Post the deviations on the ticket and move on to <next phase>?" One
  comment, headed **Product scope - deviations from the body, approved by <engineer> on <date>**,
  holds only those, each provenance-tagged, and what was deferred. What the body already says stays
  out of it. Edit the description only when asked. For a stub or napkin ticket, also offer to drop
  that marker from its title.
- **Nothing departed from the body** - understanding the ask, or answers the body already holds:
  "Move on to <next phase>?" - nothing to record.

**Mocks are suggested here, never forced** - the engineer picks mocks or the spec:
- **A new page:** suggest a mock, every time.
- **A UI change** to a page people already use: recommend one.
- **An entirely new MCP tool,** or a Slack message or interaction from Astrid whose wording needs
  vetting: offer one.
- **Field changes** - new fields, renamed ones, new values - are not mock work. Their fields are
  listed in the spec's requirements, so the move goes straight to the spec.

With a suggestion, the one question carries both roads: "Move on to mocks (recommended: this
changes the dealer profile), or straight to the spec?" - led by "Post the deviations on the
ticket and" when there are any. Without one, it names the spec - or, for a ticket that changes no
behavior a spec states or should state, the architecture, saying the spec is skipped (SKILL.md,
"Which phases run"). Stopping here is fine too: scoping without building is a normal session.

On the yes, the ticket moves to **Product Scoped** - ready to spec, with no spec branch yet
*(explicitly defined by engineer)*; a new story right after it is filed. A ticket filed while a
product requirement is still open at its core - a TODO the engineer will answer, a stub - stops at
Analysis instead *(explicitly defined by engineer)*. A title tag that no longer says what is left,
or the older "(Engineering)", is fixed in the same move: "Retag it (Closed Engineering) and move on
to the spec?"
