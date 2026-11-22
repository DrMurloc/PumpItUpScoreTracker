# Blueprints

A blueprint is a feature proven in Astrid, the codebase that grew out of this one, written up for building here. It is
not a spec: it describes something this repo doesn't do yet.

- **What one holds:** what the feature is for PIU, how it works in Astrid, how it would land here (placement under
  [CLAUDE.md](../../CLAUDE.md)'s layer rules, storage, the nine locales, the public site), the checks that would hold it,
  draft requirements in the spec shape, a build order, and the owner's open decisions.
- **When it goes:** on the branch that builds the feature, its draft requirements become the feature's spec in
  [docs/specs/](../specs/README.md) - spec-first, as [SPECS.md](../SPECS.md) says - and that pull request deletes the
  blueprint. Nothing built is described here.
- **Nothing else goes here.**

| Blueprint | What it would add |
|---|---|
| [page-anchors.md](page-anchors.md) | Anchor words in the markup (`data-tour`, `data-decision`, `data-tip`) that name a page's controls, and the map read from them: what the help assistant and the page intro point at |
| [help-assistant.md](help-assistant.md) | A help box: ask how to do something on the page you're on, get the steps, and have Show me ring each control as you do them |
| [page-intro.md](page-intro.md) | A first visit's intro card over a page, then a short tour of its controls, then tips you can hide; Replay on the Account page |
