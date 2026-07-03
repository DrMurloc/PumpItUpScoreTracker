# API

High-level map of the HTTP surface. **Swagger is the source of truth for request/response shapes**: browse `/swagger/ui` on the live site (or locally while running the app) — the OpenAPI document lives at `/swagger/v1/swagger.json`.

## Authentication

All partner endpoints use **HTTP Basic auth with your API token as the password** (username is ignored):

```
Authorization: Basic base64("anything:<your-api-token>")
```

Tokens are issued per-user on the **Account page** of the site. Endpoints are marked `[ApiToken]`; the scheme resolves the token to a user, so every call runs in that user's context.

## The stable partner surface — `api/*`

These endpoints are the contract for community tool makers. Their exact JSON wire shapes are pinned by approval tests (`ScoreTracker.Tests.Api`) — a breaking change here is treated as breaking-change review, not a casual edit.

| Area | Route | What's there |
|---|---|---|
| Charts | `api/charts` | Paginated chart listing by mix/level/type; `api/charts/random` for weighted random draws |
| Phoenix scores | `api/phoenixScores` | GET your recorded scores (paginated); POST a single best attempt; POST `import` to trigger an official-site import with your game account credentials |
| Tier lists | `api/tierlist` | Four rankings per level+chart type: `scores`, `officialscores`, `passcount`, `popularity` |
| Weekly charts | `api/weeklyCharts` | The current weekly challenge board and player scores on it |
| Tournaments | `api/tournaments` | Tournament list; `api/tournaments/{id}/matches` for bracket matches, filterable by phase/state |

## NOT the partner surface

- **`dev/export/*`** — raw table exports that exist solely so a local development copy of the site can populate itself (see [HOW-TO-RUN.md](HOW-TO-RUN.md)). They serialize physical table rows and **change without notice — including breaking changes — whenever the schema does**. Hidden from Swagger, not covered by the wire-shape tests. **Integrators must not build against these.**
- **UI-supporting controllers** — `login/*` (OAuth challenge/callback + the dev-only backdoor), `logout/*`, `culture/Set` (locale cookie), `sitemap.xml`, and `api/admin/*` (admin diagnostics). These serve the Blazor app, not API callers.

## Conventions

- Controllers are thin: every action dispatches a MediatR query/command — no business logic lives in the controller layer.
- CORS: partner endpoints allow cross-origin calls via the `API` policy.
- Building a PIU tool? You don't need to build your own importer — ask on [Discord](https://discord.gg/AvS5PxnvSN) about the score-import webhooks and existing integration patterns.
