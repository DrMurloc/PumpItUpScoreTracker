# Jira helper brief

A small helper agent (model `sonnet`) does the run's Jira writes, because a transition's response echoes
the whole ticket. Write the text to a file first, then hand the helper the file and one of the jobs
below. Every brief starts:

> Mechanical Jira writes, using the Atlassian MCP tools (cloudId `<site>.atlassian.net`). Load what
> you need with ToolSearch. The transitions list is the operation `listJiraIssueTransitions` through
> `executeRead`. Change nothing else on any ticket. If a step fails, stop that step and report it.

## Checking a write
After every write, check the text as the tool returns it, reading it back once if the response lacks
the body:
- every bold lead is bold through its end (a code span inside one ends the bold early);
- lists keep their numbers and nesting;
- links are links.

If a shape broke, fix the markdown and edit the same comment or description in place, with its
commentId. Never post a second copy.

## Jobs

- **Post a comment:** a NEW comment from `<file>`, in markdown, exactly as written.
- **Edit a comment in place:** comment `<id>` on `<KEY>`, with the full new body from `<file>`.
- **Edit a description or a summary:** `editJiraIssue` with only that field.
- **File a ticket:**
  - project DO, the issue type, and parent `<epic or story>`, set at creation;
  - the summary and description from `<file>`;
  - read it back, and report the key.
- **Walk a story.** List the transitions at each status, confirm the id leads where expected, then
  transition with only that id:
  - to start: NEW, then [221] Analysis, then [31] Product Scoped, then [51] READY FOR WORK, then
    [141] Development;
  - to close, merged dark: Development, then [81] QA, then [171] PM APPROVAL, then [301] TOGGLE
    RELEASE, then [201] Closed;
  - to close, merged live: PM APPROVAL, then [181] Pending Release, then [201] Closed.

  Pass `fields: {"resolution": {"id": "10000"}}` (Done) only on the Closed transition, leave the
  optional finance and PI fields empty, and read back the status and resolution.
- **Walk a subtask the short way:** NEW, then its in-progress status when its build starts, then
  Closed with Resolution Done when its PR merges. Take the shortest path the transitions list offers,
  skipping the granular steps.
- **Close as a duplicate:** add an issue link of type Duplicate, then walk to Closed with resolution
  Duplicate (`10002`).

## Report back
One line per step: done, or the error. For a ticket, give its key and final status, and say what you
fixed after a read-back.
