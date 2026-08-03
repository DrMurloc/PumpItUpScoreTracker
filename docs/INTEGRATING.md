# Building a tool on PIU Scores

The maker's manual for `api/v2`. It lives here rather than on the site because it is prose: every
on-site string ships in nine locales, and a reference nobody translates does not belong in that
pipeline. What *is* on the site is the part that needs your keys and your tool —
[/Developers](https://piuscores.arroweclip.se/Developers).

The wire shapes are in Swagger at [`/swagger`](https://piuscores.arroweclip.se/swagger), and
Swagger is the source of truth for them. This document is the part Swagger cannot tell you: what the
values *mean*, and which mistakes are easy to make.

---

## 1. The rules

These are canonical. The site renders them in nine languages on the registration screen; this
English version is the one that governs where a translation and it disagree.

> ### DrMurloc's Rules for Integrated Toolmakers
>
> *PIU Scores was built on the principle that we, Pump It Up players, are all one community, divided
> only by physical distance. Aim to connect that community.*
>
> **1. No money in it.** Free to use and free of ads. A tip jar or community fund covering your
> hosting is fine — a supporter tier that unlocks features is not. Anything built to turn a profit
> gets removed.
>
> **2. Built for the community.** Tools that help players understand the game — score distributions,
> progress, analysis anyone can use. A tool built for one person's edge, like scouting opponents
> before a tournament, gets removed, and its maker does not get to make more.
>
> **3. Stay reachable.** Toolmakers stay in the PIU Scores Discord, so I can message you when
> something goes wrong.
>
> **4. Stay in good standing.** The server rule is "Don't Be An Asshole", and it covers toolmakers
> too. I can remove a tool for any reason — including anything discriminatory, or aimed at excluding
> or antagonising any Pump It Up community members.

### What registration asks for

Two fields, neither required to *create* a tool, both required before it can reach anyone but you:

- **A public source repository.** GitHub, GitLab, Codeberg, your own gitea — anywhere a player can
  read it without an account. A listed tool links straight to it. The link is fetched anonymously
  and has to answer, because a private repository answers fine to *your* browser and 404s to every
  player being invited to read it.
- **Your Discord handle.** Only DrMurloc sees it. It is how you get told when your tool breaks.

Until both are in place your tool still works completely — keys, webhooks, the API, all of it — but
only against your own scores. Invite links and listing are closed until then. The console says so
and names what is missing.

Changing the repository on a listed tool sends it back for review, the same as changing its name.
Passing review with one repository and swapping it afterwards is the thing review exists to catch.

---

## 2. Seven steps

1. **Create your tool.** It starts private and fully working — invite links, keys and webhooks all
   function. Listing only puts you in the public directory.
2. **Create an API key.** Send it as `Authorization: Bearer piu_scores_live_…`. It is shown once; only a hash is stored. Two can be live at a time so you can rotate without downtime.
3. **You are player one.** Creating a tool connects your own account to it, so you always have a real
   player to test against.
4. **Pull your own scores.** `GET /api/v2/players/me/scores?mix=Phoenix`.
5. **Receive imports.** Point PIU Scores at a URL, then send yourself a test delivery.
6. **Invite a friend.** Share an invite link. Anyone with it can connect, public profile or not.
7. **Ask to be listed.** A listed tool appears in the directory and can accept players who share
   with all tools. You keep working while it is reviewed.

---

## 3. Authentication

```
Authorization: Bearer piu_scores_live_8Kd2Qm4xV7pR1nZaL9cE6yTb3WsHfJ0u
```

A **tool key** authenticates the tool. It reaches every player who granted it access, and it is
never a player itself — `GET /api/v2/players/me` is an error for a tool, because a tool has no self.

A **personal token** (the GUID from your Account page, sent as HTTP Basic) authenticates one person
and reaches nobody else. It still works, unchanged, on both v1 and v2.

In Swagger: press **Authorize** and paste the key into **toolKey** — the key itself, without the
word `Bearer`.

A tool key also works in the **password position of Basic auth**, with anything for the username.
That is not the documented form and Bearer is what you should ship, but v1 taught everyone to put
their credential there and it is not worth a 401 to be strict about.

---

## 4. The five things that trip people up

### 4.1 `mix` is required, and there are thirty of them

Every mix-scoped endpoint needs it. There is no default — v1 defaults to Phoenix forever because it
promised byte-identical responses to integrations that predate Phoenix 2, and repeating that in a new
version would plant the same rot for whatever comes next.

Call `GET /api/v2/mixes` first. Use the `name` field verbatim (`Phoenix2`, not `Phoenix 2`).

### 4.2 Half the mixes score differently

`scoringModel` on the mix, and on every score page, is `phoenix` or `legacy`.

- **`phoenix`** — Phoenix and Phoenix 2. A 1M-scale `score`, and a `plate`.
- **`legacy`** — everything older. A `letterGrade`, `isBroken`, and an *optional* era-scale `score`
  that does **not** compare to a Phoenix number.

Reading a Fiesta EX record as a Phoenix score gives you a plausible, wrong answer. Branch on
`scoringModel` before you touch `score`.

### 4.3 A null is not a zero

- `plate` is `null` when `isBroken` — the game awards no plate for a failed stage.
- `judgments` is `null` when the source never carried a breakdown (a CSV import, a hand-entered
  score). Zeros there would read as a perfect game.
- `pumbility` is `null` on legacy mixes, which have no PUMBILITY formula.

### 4.4 `recordedAt` is when the row was *written*

Not when the play happened. Nobody here knows when the play happened — the official site does not
report it reliably. There is exactly one date on a score and this is it.

### 4.5 Cursors belong to their filters

Follow the `next` link; do not build a cursor. A cursor carries a fingerprint of the filters it was
issued under, and replaying it against different filters is a `400` rather than quietly shifted rows.

---

## 5. Reading scores

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

## 6. Receiving imports

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

### Verifying your endpoint (do this first)

**Nothing is sent to a URL you have not proven is yours.** There are two steps, and the order
matters.

**1. Register a verification secret** on `/Developers` — type one or press Generate — and put the
same string in your handler. Only a SHA-256 hash of it is stored, so keep your copy.

**2. Press Verify.** PIU Scores POSTs:

```
POST <your url>
{ "type": "url_verification" }
```

Answer `200` with your secret in the body — bare, quoted, or inside JSON, all fine:

```
200 OK

vfy_8f2a91c04e
```

Note what the request does **not** contain: your secret. That is the entire point. An earlier
revision sent a challenge for you to echo, which anything able to receive the request could satisfy
— including whatever a hijacked DNS record happened to point at. Now answering correctly requires
already knowing something that was never transmitted.

Only then do deliveries start. **Changing the URL or the secret clears verification** — a proof that
outlives the thing it was a proof of is worse than no proof.

Why this exists at all: a typo in that box would otherwise post a player's scores, on a
schedule, to whoever happens to own the host you mistyped. This turns that into a failed save.

If verification fails you get the reason and your server's own status code. A `200` without the
secret is the interesting one: the URL is alive, but whatever answered does not hold your secret.

**The webhook URL has to be public.** Loopback and private-network addresses are refused, checked
against what the host actually resolves to — from PIU Scores' servers those point at its own infrastructure, not
yours. To develop against `localhost`, run PIU Scores locally ([HOW-TO-RUN.md](HOW-TO-RUN.md)); the
local run allows them.

### Verifying it came from PIU Scores

**Set a header on `/Developers`** — any name, any value — and it is sent verbatim on every delivery
over TLS. Check it in your handler and reject anything without it. That is one `if`, and it is the
whole mechanism. It is stored encrypted rather than hashed, because unlike your verification secret
it has to be sendable.

**These are two different values and must stay that way.** The header travels to your server on
every call, so anyone who receives one delivery has read it — handing it back at verification time
would prove nothing. The verification secret goes the other way and never leaves PIU Scores.

Optional for score push and player ping — if someone guesses your URL the worst they do is write
junk into your own database, which is yours to guard. **Required for PIUGame session mode**, where
the thing arriving is a live piugame.com credential and an endpoint that cannot tell a PIU Scores call from
anyone else's has no business receiving one.

The header is sent on the verification POST too, so your handler can check it from the very first
request.

There is no request signature. An earlier revision signed each body with HMAC-SHA256; it was removed
because TLS already authenticates the transport, and the marginal protection did not justify a
crypto layer on both sides of the wire.

### Retries and duplicates

`X-PIU-Delivery-Id` is stable across retries — **dedupe on it**. There are five attempts, with
exponential backoff over roughly an hour, then give up and leave the failure in your activity log.

### PIUGame session mode is different

It hands over a live credential, so:

- Every player must agree to it individually. It is never available through "share with all tools",
  and you can only switch a tool into it while no players are connected.
- The body is never written down. There is **no retry and no replay** for a session delivery — if your
  server is down when it fires, it is gone. Your activity log shows delivered or failed and nothing
  behind it.
- Your header is **required** in this mode, not optional. A live credential is not handed to an
  endpoint that has no way of telling a PIU Scores call from anyone else's.

---

## 7. Limits, and how not to hit them

| | |
|---|---|
| Tool key | 600 requests/minute |
| Personal token | 60 requests/minute |
| Response on exceeding | `429` with `Retry-After` and `RateLimit-*` headers |

Two habits that matter more than the numbers:

- **Cache the catalog.** Charts and songs change a few times a year. Send `If-None-Match` with the
  `ETag` you were given, and a 304 costs nobody anything.
- **Do not poll if you have webhooks.** A delivery already tells you who imported and what changed.
  Re-pulling every player on a timer is the one pattern that will get a tool throttled.

---

## 8. Errors

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

## 9. Running the site locally

If you are building something substantial, you can run PIU Scores on your own machine and point
webhooks at `localhost` — see [HOW-TO-RUN.md](HOW-TO-RUN.md). It needs Docker.

Most makers never need this. Between the test delivery and replay on `/Developers`, the usual
reasons to reach for a local instance are already covered.

---

## 10. What is not here

- **Writes.** Every mutation stays on v1 with a personal token. There is no way for a tool to record
  a score or change anything on a player's account, and that is not an oversight.
- **Community data.** Memberships and community boards are other people's data; a share covers the
  sharer, not their crew.
- **Player search.** You see who shared with you. Nothing enumerates the site.

---

## 11. Asking

`#tool-makers` on [Discord](https://discord.gg/AvS5PxnvSN). It is the fastest way to get unstuck, and
where breaking changes are announced first.
