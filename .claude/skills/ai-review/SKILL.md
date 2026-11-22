---
name: ai-review
description: Review a change the way the ticket loop's review does - a blind bug reviewer and a flag reviewer, then a skeptic that rules on every finding - and sort what it keeps into send back (the author fixes it, once, and no one else needs to see it) and escalate (it needs a person's eyes, sorted by how soon). Use it when someone says "review this change", "review my branch", "AI review PR 23500" or "run the review on <branch>", or wants it run on a teammate's change before reading it. It runs locally and posts nothing unless asked.
---

# AI review

The ticket loop's review ([steps/6-execution.md](../ticket-loop/steps/6-execution.md), "Review - once"), run
on any change: this branch, another branch, or a pull request. The roles' briefs are the review's own, in
[ticket-loop/review/](../ticket-loop/review/): reviewer.md, flags.md and skeptic.md. Quality comes first: the review is sized for what it catches, not for what it spends.

## 1. Find the change

- **The change** is the diff from the branch it merges into (main unless you're told otherwise) to its head:
  `git fetch origin <target>`, the merge base, and `git diff <base>..<head>` written to a file in the scratchpad.
  For a pull request, read its source and target branches with
  `az repos pr show --id <id> --org https://dev.azure.com/<org>`.
- **The ticket** is the key in the branch name or the commit messages. Write its description and comments, the
  approved pre-review among them, to a scratchpad file (`getJiraIssue`, `listJiraIssueComments`). With no ticket,
  say so: the review runs on the change alone.
- **REVIEW.md** is the target branch's copy: `git show origin/<target>:REVIEW.md` to a scratchpad file, or
  note that the target has none.
- **The toggle:** when the ticket's Decisions locked say the epic ships dark, its toggle's name.

## 2. The review: two blind sub-agents at once

The bug reviewer and the flag reviewer, each a general-purpose sub-agent that sees only what you hand it - never
this file, this conversation or another role's work:
- its one line: "you are a bug reviewer in a review: read <absolute path to review/reviewer.md> and do
  your part", or the same for the flag reviewer and flags.md;
- the diff file, and how to read the head: this worktree checked out on it, or `git show <head>:<path>`;
- the ticket file, the REVIEW.md file or "none", and the toggle;
- where to write its reply, and to answer you in one line.

One bug reviewer, never two *(explicitly defined by engineer, 2026-11-07: a second is "just excessive")*.

## 3. The skeptic

- Write both lists into one threads file, each finding a thread with an id: the bug reviewer's regressions
  and bugs, and the flag reviewer's missed criteria, scope creep and flags.
- The skeptic (skeptic.md), handed the threads file and what the reviewers got, the toggle included, replies
  and rules on each, in a file: each reply ends in its **Verdict:** line, and a file has no status to set.
  It returns its list: per thread, the id, the verdict and one line, and for each Escalate its bucket and
  its three parts. Read each Decline's line.
- With no threads, it doesn't run.

## 4. The report

In the chat, in this order:
1. **Escalate - where a person should look.** The escalations under their buckets, in this order *(explicitly
   defined by engineer, 2026-11-13)*: **Fix or review before go-live** when the change ships behind a toggle,
   or **before deploy** when it doesn't; **Not a blocker, still worth your eyes**; **Backlog, don't stress
   about it**. Each in three short parts, a few lines: What, Why, Suggested action, written as
   [review/writing.md](../ticket-loop/review/writing.md) says.
2. **Send back - for the author.** Each regression, bug and missed criterion to send back, with its fix, as
   one list the author works through once. No one else needs to see it.
3. The declined findings, counted in one line, and the flag reviewer's **Couldn't check** line.

Name the scratchpad folder that holds every role's file. Post on a pull request only when asked, and then as the
ticket loop's review posts: a thread per regression, bug, missed criterion and scope creep (**Reviewer:**)
and per flag (**Flag:**).

## Never

- Change, commit or push anything in the change under review.
- Show one role's work to another, except the threads file to the skeptic.
