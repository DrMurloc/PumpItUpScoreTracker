# Review rules

The rules every change to this repo holds to. The AI review reads them from `main`'s copy of this file,
so a pull request's own edit to it applies only once it merges. Each rule adds a check; none skips or
softens one.

- **A UI string lands in every locale.** A change that adds or changes a UI string adds or changes it in
  all nine locales in the same pull request, in each locale glossary's terms (CLAUDE.md, UI conventions,
  Localization). A string missing from any locale is a bug.
- **A doc the change makes untrue is fixed in the same pull request** - every doc in CLAUDE.md's
  Documentation set, and [docs/API.md](docs/API.md) above all.
