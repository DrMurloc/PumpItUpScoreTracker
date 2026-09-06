# March of Murlocs — mocks

The mocks live as Artifacts, linked from the header of [the plan](../march-of-murlocs.md); the
decisions they settled are its §11.9 and the build scope its §12. They are not checked in: each
page embeds its own art (about 8 MB across the seven), so the repo carries the spec and the links.

| | Surface | Route |
|---|---|---|
| 1 | Season | `/MarchOfMurlocs` |
| 2 | Session Breakdown | `/MarchOfMurlocs/Session/{id}` |
| 3 | Submit | `/MarchOfMurlocs/Session/{id}/Edit` |
| 4 | Planner | `/MarchOfMurlocs/Planner` |
| 5 | Discord card | — |
| 6 | Past seasons | *a dialog, no route* |
| 7 | My Sessions on-ramp | `/Player/{id}/Sessions` |

The build pipeline (templates, the shared skin that mirrors `site.css`, the SQL-pull → JSON
scripts, the rest-chart analysis) is in the owner's `Downloads\mom-mocks-v2-2026-09-05\`, README
inside. Every figure in the mocks is production-synced data.

The August set that used to sit here was removed on 2026-09-05: its landing mock had never
rendered (an unterminated string in its script) and none of the six declared a charset.
