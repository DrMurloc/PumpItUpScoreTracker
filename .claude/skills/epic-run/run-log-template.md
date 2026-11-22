# <EPIC> run - <YYYY-MM-DD>

Copy this to `notes/<epic-slug>/<YYYY-MM-DD>-run/run-log.md` in the folder above your git repos, and
keep it current after every step. It is the run's state of record: a new session resumes from it.

## RESUME HERE - state as of <date> (<running | paused: what the engineer said | finished>)

**Where things stand**

| # | Ticket | Branch | PR | State |
|---|---|---|---|---|
| 1 | <KEY> <title> | `<branch>` (off <main or the ticket before>) | <id or -> | <spec writing, spec in review, planning, building, review, ready to merge, merged, closed> |

**Next steps**, in order:
1. <the next action, with what it waits on>

**Worktrees**
- Build: `<path>`, on `<branch>`.
- Throwaway (to remove at the end of the run): <`agent-<id>` (role, detached at <sha>)>, ...

**Escalations** - each raised in the chat and added to the wrap-up ticket (<KEY>, filed with the first one; a
run of one or two tickets files none, and gives them in its final report).

| # | Ticket / PR | Kind | Bucket | What | The engineer's answer |
|---|---|---|---|---|---|
| 1 | <KEY> / <PR> | <regression, bug, missed criterion, scope creep, flag, scope deviation> | <before go-live or before deploy, worth your eyes, backlog> | <one line> | <answer and date, or open> |

---

## The run

- **Epic:** <EPIC>, <dark behind `<toggle>` | live>.
- **Send list and order:** <the ready tickets, in run order; name any moved to READY FOR WORK at the start>.
- **Specs:** the spec changes the engineer approved AI to write, each in its ticket's turn, and any
  only an exploration can settle (and why).
- **Closed loops:** <per ticket: the swarm's shape, the most rounds, what good enough is - signed off
  <date> | none>.
- **Out of the run:** <stories and why>.
- **Decisions taken in the start conversation:** <one line each>.

## Per ticket

### <KEY> <title>

- **Spec:** <the spec commits on `<branch>`, reviewed <by the engineer | on the engineer's
  delegation> <date> | none needed>. Jira comment: <spec id>.
- **Plan:** `<KEY>-plan.md`, reviewed <date>. Jira comments: <pre-review id>, <commit order id>.
- **Subtasks:** <one per PR when the plan split the ticket, with their keys>.

| PR | Branch | ADO PR | Head | Tiers | Review |
|---|---|---|---|---|---|
| <n> | `<branch>` | <id> | <sha> | <unit, bUnit, integration, Playwright counts> | <threads, verdicts, fixes, merged> |

- **Launch notes:** <prod config deletes, destructive migrations, deploy order>.
- **Completion:** <comment id, and the close path taken>.
