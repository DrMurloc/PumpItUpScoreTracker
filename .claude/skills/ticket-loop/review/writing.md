# Review - feedback writing rules

Everything a review writes - a finding, a flag, a reply, a decision, a question - is read by someone who
hasn't read the code and has seconds to decide whether it matters. Write each one in this order:

1. **What it is** - the first line, in plain words: what is wrong or what changed, and where a person would
   notice it. A reader who stops after this line knows what they are looking at.
2. **Why it reaches the reader** - one sentence: what is at stake, or what they have to decide or do, and
   for whom. Never how the review works - not "per the brief", "the rule says", "this is a flag because".
3. **Details** - only what someone needs to act on it or check it, as a short list: where (file and line),
   the approved text it departs from, quoted short, the input that triggers it, the fix, who should look.
   Leave them out when there is nothing to add. An escalation's third part is its **suggested action** - the
   concrete next step - and any details follow it *(explicitly defined by engineer, 2026-11-13)*.

What keeps it readable:
- **The substance leads, never the label.** A category or code may open the first line, but the words after
  it say what happened: "Weakened security: any lobby member can read another lobby's answers", not
  "Weakened security - authorization".
- **Say it once.** The first line isn't repeated in the why or the details, and a reply never restates the
  finding it answers.
- **No preamble or hedging:** no "I found", "after reviewing", "it's worth noting", "potentially".
- **Plain words in the first two lines:** no class, file, method or setting names - those go in the details.
- **Short:** the first two lines fit in about 40 words, and a details list rarely needs more than five items.
- **A question leads with the decision** - "Ship the new review wording as is?" - then, in one sentence, why
  it is the reader's call, then the options, the one you recommend first.
- **A reply that decides something leads with the decision** - "Fix in this PR: add the missing answer
  snapshot" - then why, then the details the decision needs.
