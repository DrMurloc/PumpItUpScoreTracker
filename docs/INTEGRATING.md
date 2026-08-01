# Building a tool on PIU Scores

The maker's manual for `api/v2`. It lives here rather than on the site because it is prose: every
on-site string ships in nine locales, and a reference nobody translates does not belong in that
pipeline. What *is* on the site is the part that needs your keys and your tool —
[/Developers](https://piuscores.arroweclip.se/Developers).

The wire shapes are in Swagger at [`/swagger/ui`](https://piuscores.arroweclip.se/swagger/ui), and
Swagger is the source of truth for them. This document is the part Swagger cannot tell you: what the
values *mean*, and which mistakes are easy to make.

---

## 1. Seven steps

1. **Create your tool.** It starts private and fully working — invite links, keys and webhooks all
   function. Listing only puts you in the public directory.
2. **Create an API key.** Send it as `Authorization: Bearer pst_live_…`. It is shown once; we store
   a hash. Two can be live at a time so you can rotate without downtime.
3. **You are player one.** Creating a tool connects your own account to it, so you always have a real
   player to test against.
4. **Pull your own scores.** `GET /api/v2/players/me/scores?mix=Phoenix`.
5. **Receive imports.** Point us at a URL, then send yourself a test delivery.
6. **Invite a friend.** Share an invite link. Anyone with it can connect, public profile or not.
7. **Ask to be listed.** A listed tool appears in the directory and can accept players who share
   with all tools. You keep working while it is reviewed.

---

## 2. Authentication

```
Authorization: Bearer pst_live_8Kd2Qm4xV7pR1nZaL9cE6yTb3WsHfJ0u
```

A **tool key** authenticates the tool. It reaches every player who granted it access, and it is
never a player itself — `GET /api/v2/players/me` is an error for a tool, because a tool has no self.

A **personal token** (the GUID from your Account page, sent as HTTP Basic) authenticates one person
and reaches nobody else. It still works, unchanged, on both v1 and v2.

In Swagger: press **Authorize**, and paste `Bearer pst_live_…` into the value field.

---

## 3. The five things that trip people up

### 3.1 `mix` is required, and there are thirty of them

Every mix-scoped endpoint needs it. There is no default — v1 defaults to Phoenix forever because it
promised byte-identical responses to integrations that predate Phoenix 2, and repeating that in a new
version would plant the same rot for whatever comes next.

Call `GET /api/v2/mixes` first. Use the `name` field verbatim (`Phoenix2`, not `Phoenix 2`).

### 3.2 Half the mixes score differently

`scoringModel` on the mix, and on every score page, is `phoenix` or `legacy`.

- **`phoenix`** — Phoenix and Phoenix 2. A 1M-scale `score`, and a `plate`.
- **`legacy`** — everything older. A `letterGrade`, `isBroken`, and an *optional* era-scale `score`
  that does **not** compare to a Phoenix number.

Reading a Fiesta EX record as a Phoenix score gives you a plausible, wrong answer. Branch on
`scoringModel` before you touch `score`.

### 3.3 A null is not a zero

- `plate` is `null` when `isBroken` — the game awards no plate for a failed stage.
- `judgments` is `null` when the source never carried a breakdown (a CSV import, a hand-entered
  score). Zeros there would read as a perfect game.
- `pumbility` is `null` on legacy mixes, which have no PUMBILITY formula.

### 3.4 `recordedAt` is when *we* wrote the row

Not when the play happened. We do not know when the play happened — the official site does not tell
us reliably. There is exactly one date on a score and this is it.

### 3.5 Cursors belong to their filters

Follow the `next` link; do not build a cursor. A cursor carries a fingerprint of the filters it was
issued under, and replaying it against different filters is a `400` rather than quietly shifted rows.

---

## 4. Reading scores

```
GET /api/v2/players                       → who shared with you
GET /api/v2/players/{id}                  → one player's profile
GET /api/v2/players/{id}/scores?mix=      → best attempts
GET /api/v2/players/{id}/sessions?mix=    → import and play sessions
GET /api/v2/players/{id}/journal?mix=     → every attempt, with judgments
```

`GET /api/v2/players` is where you start: without it you have no way to learn who consented.

A player who has not shared with you is a **404, not a 403**. That is deliberate — a 403 would
confirm the account exists, which would make this an enumeration oracle.

**Incremental sync without webhooks:** pass `recordedAfter` to the scores endpoint. Store the highest
`recordedAt` you have seen and pass it back next time. Most tools never need anything else.

---

## 5. Receiving imports

Three modes, set on `/Developers`:

| Mode | You get |
|---|---|
| **Player ping** | "this player imported", and nothing else |
| **Score push** | the changed scores, 100 per delivery, with `next` for the rest |
| **PIUGame session** | the piugame.com session key, so you run your own scrape |

### The delivery body

```jsonc
{
  "deliveryId": "d-4f819c",
  "schemaVersion": 1,
  "sentAt": "2026-08-01T14:21:55Z",
  "test": false,
  "player": {
    "mix": "Phoenix",
    "scoringModel": "phoenix",
    "userId": "9f14c0e2-…",
    "username": "DrMurloc",
    "gameTag": "MURLOC#1"
  },
  "sessionId": "…",
  "changes": [ … ],
  "next": null
}
```

### Verifying it came from us

Every delivery carries **both**, so pick whichever suits you:

- **A header you chose.** Set a header name and value on `/Developers`; we send it verbatim. This is
  one `if` in your handler and it is what most tools use.
- **`X-PIU-Signature: t=<unix>,v1=<hex>`** — HMAC-SHA256 over `"<t>." + rawBody` with your signing
  secret. The timestamp is inside the signed payload, so a replay does not verify.

> ⚠ If your signature check fails, you are almost certainly hashing a **re-serialized** copy of the
> body rather than the raw bytes you received. Parsing and re-stringifying JSON changes whitespace
> and key order. Hash the bytes off the wire.

### Retries and duplicates

`X-PIU-Delivery-Id` is stable across retries — **dedupe on it**. We attempt five times with
exponential backoff over roughly an hour, then give up and leave the failure in your activity log.

### PIUGame session mode is different

It hands over a live credential, so:

- Every player must agree to it individually. It is never available through "share with all tools",
  and you can only switch a tool into it while no players are connected.
- We never write the body down. There is **no retry, no replay, and no signature echo** for a session
  delivery — if your server is down when it fires, it is gone. Your activity log shows delivered or
  failed and nothing behind it.

---

## 6. Limits, and how not to hit them

| | |
|---|---|
| Tool key | 600 requests/minute |
| Personal token | 60 requests/minute |
| Response on exceeding | `429` with `Retry-After` and `RateLimit-*` headers |

Two habits that matter more than the numbers:

- **Cache the catalog.** Charts and songs change a few times a year. Send `If-None-Match` with the
  `ETag` we gave you and a 304 costs neither of us anything.
- **Do not poll if you have webhooks.** A delivery already tells you who imported and what changed.
  Re-pulling every player on a timer is the one pattern that will get a tool throttled.

---

## 7. Errors

`application/problem+json`, RFC 9457:

```json
{
  "type": "https://piuscores.arroweclip.se/errors/mix-required",
  "title": "The mix parameter is required.",
  "status": 400,
  "detail": "Valid values: Phoenix, Phoenix2, XX, FiestaEx, …",
  "instance": "/api/v2/charts"
}
```

Branch on `type`. It is the stable part; `title` and `detail` are written for humans and may be
reworded.

---

## 8. Running the site locally

If you are building something substantial, you can run PIU Scores on your own machine and point
webhooks at `localhost` — see [HOW-TO-RUN.md](HOW-TO-RUN.md). It needs Docker.

Most makers never need this. Between the test delivery, the signature echo and replay on
`/Developers`, the usual reasons to reach for a local instance are already covered.

---

## 9. What is not here

- **Writes.** Every mutation stays on v1 with a personal token. There is no way for a tool to record
  a score or change anything on a player's account, and that is not an oversight.
- **Community data.** Memberships and community boards are other people's data; a share covers the
  sharer, not their crew.
- **Player search.** You see who shared with you. Nothing enumerates the site.
- **`dev/export/*`.** Raw table rows for the local dev harness. They change without notice, including
  breaking changes. Do not build against them.

---

## 10. Asking

`#tool-makers` on [Discord](https://discord.gg/AvS5PxnvSN). It is the fastest way to get unstuck, and
where breaking changes are announced first.
