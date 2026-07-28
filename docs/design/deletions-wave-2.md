# Deletions — Wave 2

Deletions that **wave 1 could not finish** because they need a data migration or a coordinated
multi-file pass. Scope is narrow on purpose: if something is a rework, a rearchitecture, or a
feature we intend to keep, it does not belong here — it belongs in its own design doc.

Not in this file, and why:

| Not here | Where it lives |
|---|---|
| Community difficulty voting | Not being deleted — the ecosystem returns for legacy mixes |
| Chabala tier lists | Not being deleted — rework, [tier-lists-overhaul.md](tier-lists-overhaul.md) |
| Chabala skills | [nuke-old-skill-categories.md](nuke-old-skill-categories.md), its own 10-commit plan |
| `GetRandomChartsQuery` → Randomizer | Rearchitecture, not a deletion |
| Repository injection into Web | Architecture improvement, not a deletion |

---

## 1. `TournamentType` cannot shrink yet

After wave 1, `TournamentType.CoOp` has zero consumers — its only reference was
`Tournaments.razor:247` routing to the already-dead `/Tournament/{id}/Brackets`.

But the enum is persisted in `scores.Tournament.Type` and CoOp rows exist (BITE 7 - Co-Op).
Removing a member shifts the ordinal values of everything after it unless every member is
explicitly valued, so this needs a data migration, not an edit.

`TournamentType.Match` **stays regardless** — CEO 2026: Project Storm is `Type=Match` and live.

Natural home: the qualifiers + randomizer tournament-ecosystem merge, which may retire
`TournamentType` altogether.

---

## 2. Orphaned resx keys

Deleted markup leaves its localization keys behind in all nine locales. They are inert —
`LocalizationKeyTests` only checks that every locale carries the same key set as `en-US`, so
leaving them is safe and removing them means nine coordinated resx edits.

Given the documented resx hazards (case-insensitive `GenerateResource` dedup, splice-not-XML
editing, the merge tail seam), this is its own mechanical pass with its own verification — never
a rider on a deletion PR.
