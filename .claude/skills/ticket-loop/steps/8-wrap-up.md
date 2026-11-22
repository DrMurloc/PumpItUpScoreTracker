# Phase 8 - Wrap up

**Runs** once the field test is done - the hand-off; the iteration is done. **Decides** nothing new:
the PR goes to review merge-ready, and the ticket gets its record and closes. **Nothing here waits on
the engineer:** each step below is done, not offered, and the message reports what was done.

- **Merge-ready, then hand it to review.** Main merged in, the slow tiers green on the result, every
  thread resolved, the description current. Then it goes to review: a push resets approvals, and the
  approver can't be the author or the last pusher. Never merge, complete or auto-complete the PR - an
  approver signs off and the engineer merges. A PR that is already merged skips this.
- **Post the Jira completion comment:** one plain sentence on what shipped and whether people can use
  it yet - live, or merged dark behind its toggle - for a reader outside the team; then how to test,
  the requirement IDs that landed, the decisions with their provenance tags, the deferred questions
  and their tickets, the follow-ups, and fixes to bugs that predate the ticket. Bugs the ticket
  introduced and fixed stay on the PR threads.
- **Close the ticket once the PR merges** - when the engineer says it merged, or the PR shows
  completed: walk its workflow (`getTransitionsForJiraIssue` at each status) to Closed, with
  **Resolution = Done** (id `10000`) - the API lets Closed through without one. A ship behind a toggle
  takes Toggle Release from PM APPROVAL before closing. Until the PR merges, the ticket waits; the
  session that hears it merged closes it.
- **Release:** there is no pipeline yet - `docs/CONTRIBUTING.md` says who releases.

## The message

The frame from SKILL.md, headed "Phase 8 of 8: Wrap up". Body: what was done - the PR's state (in
review, or merged), the completion comment, and the ticket's status (closed, or waiting on the merge).

**What I need from you:** only what the loop can't do - an approver for the PR while it still needs
one. Otherwise, nothing.
