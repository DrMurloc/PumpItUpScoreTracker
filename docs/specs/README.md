# Specs - the index of PIU Scores' feature specs

PIU Scores is spec-first, and the spec wins. How to write and change a spec: [SPECS.md](../SPECS.md).

## Specs

Each spec states how one feature of one bounded context behaves. One line each, grouped by
bounded-context folder: the spec, its prefix, and what it covers.

No specs yet. Until a feature has one, its design doc in [docs/design/](../design/) is its record.

## Removed

The append-only register of identifiers that are never used again: each requirement ID removed from a
spec gets `- <ID> <former name> (removed <YYYY-MM-DD>): <reason>`, ending `. Replaced by <ID>.` where a
new requirement replaces it, and each retired prefix gets `- <PREFIX> (retired <YYYY-MM-DD>): <reason>`.

Nothing removed yet.
