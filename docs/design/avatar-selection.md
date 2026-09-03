# Manual avatar selection

A player picks their own avatar on `/Account` instead of wearing whatever their last piugame
import happened to find. The pick sticks until they undo it.

---

## 1. The two states

| State | What it means | Who writes the avatar |
|---|---|---|
| **Auto** | Today's behavior, and the default for every account. | The import, on every run. |
| **Pinned** | The player chose an avatar from the catalog. | Nobody but the player. |

Auto is not "no avatar" — it is a live link to the official profile. An import while pinned still
records what it saw (`User.ImportedAvatar`), so **Back to Auto restores the real piugame picture
immediately** rather than leaving the player waiting for their next import. That is the whole
reason the imported avatar gets its own column instead of being overwritten.

## 2. The avatar is written to two places, and both are gated

This is the part that bites. The visible avatar lives in **two** stores:

- `User.ProfileImage` → the auth cookie's claims → the player page, leaderboards, Discord cards.
- A `ProfileImage` **UiSetting** → `ShellModelFactory` → the app-bar avatar in the static shell.

Before this feature, `OfficialLeaderboardSaga` wrote both, six lines apart. A pin that gated only
one of them would leave the top-right corner showing the piugame avatar while every other surface
showed the pinned one, which reads as a caching bug and is not one.

So the UiSetting write **moves out of OfficialMirror** into `UpdateUserGameProfileHandler`. One
handler owns both sinks, one flag gates both, and the Mirror never learns that pinning exists —
which is also the right vertical boundary, since the Mirror does not own user data. The Mirror
keeps writing the game tag setting, because the game tag has no manual override.

Changing the avatar needs the `refreshLogin` javascript dance the Profile tab already does for
name and country: the claim lives in a cookie that a live circuit cannot rewrite. The UiSetting
half needs no such thing — `UiSettingCacheEviction` already drops the shell's cached settings on
every settings write.

## 3. The catalog is one set of avatars in three skins

The three official avatar pages are:

| Mix | Page | Entries |
|---|---|---|
| XX | `xx.piugame.com/piu.xx/itemshop/xx_avatarshop.php` | 83 |
| Phoenix | `phoenix.piugame.com/my_page/avatar_shop.php` | 164 |
| Phoenix 2 | `piugame.com/my_page/avatar.php` | 165 |

All three need a login. The Phoenix pair renders the same `ul.data_titleList2 > li[data-name]`
markup as `my_page/title.php`, which we already parse.

**412 listed entries collapse to 182 distinct pictures and 170 avatars.** Measured, not eyeballed:
every pair of pictures was compared on the inner 40&nbsp;% so a border cannot count as a
difference. 295 of 312 pairs came in under 7 on a 0–255 mean-channel scale, then a clean cliff to
17 pairs at 19.65 and above. Nothing lands in between, so the threshold is not a judgment call.

What the differences turned out to be:

- **Phoenix draws a decorative frame that Phoenix 2 drops.** That is the entire difference for 63
  avatars; another 82 are byte-identical. The unframed Phoenix 2 art is what gets stored.
- **XX is the same art at 160×120** for 75 of its 78 shared names. Only `Hero` and `Miya` are
  genuinely different pictures there.
- **12 avatars were really redrawn** for Phoenix 2: Jeanne, Azura, Lightning, Luana, Phantom
  Thief M & Detective P, Melt, AM Corporation Research Team, Paper Farmer, Downi, CIDER, Kumomo,
  Devit's Hope. Those keep both pictures; everything else keeps one.

### 3.1 Never infer identity from a filename

The two Phoenix avatar directories **reuse ids for unrelated art**, which is the recurring avatar
bug in this codebase. `4f617606…` is **Azura** under `/avatar_img/` and **Electra**
under `/avatar_img2/`; `5f71d392…` is Phoenix's `hero` and Phoenix 2's `Electra`. Reading a
rename out of a shared filename is wrong every time. **Every join in the catalog is made on
decoded pixels**, and the seed was produced that way.

XX art therefore mirrors to its own `/avatars/xx/` prefix, exactly as Phoenix 2's does to
`/avatars/p2/`, so no id can collide across eras.

### 3.2 Names are not unique, and that is fine

The official pages themselves ship colliding names, and all of them are genuinely different
pictures:

- `Electra` appears **twice** in Phoenix 2's own list.
- `Hero` / `hero` and `Miya` / `MIYA` differ only by case.

They are stored as separate avatars with the site's exact casing. **Nothing in the catalog assumes
a unique name** — identity is the row, the name is a label and a search key. Two `Electra` tiles
sitting next to each other in the picker is correct, and a player choosing between them is
choosing between two pictures they can see.

### 3.3 Azura is the stock avatar

`4f617606…` under `/avatars/` — the file hardcoded as the fallback in `ShellModelFactory` and
`UpdateUserHandler` — is a real catalogued avatar named **Azura**. She stays pickable (owner call).
A player who deliberately picks Azura is indistinguishable from one who never imported, which is
an accepted cosmetic collision, not a bug to design around.

## 4. Storage

One table, `scores.Avatar`, owned by the **Catalog** vertical — it is game content, the same shelf
as songs, charts and videos.

| Column | Notes |
|---|---|
| `Id` | Identity PK. One row per **picture**, so 182 rows. |
| `GroupId` | Rows sharing one group are the same avatar's alternate pictures. 170 groups. |
| `Name` | The display name, site casing preserved. Not unique. |
| `ImageUrl` | The piuimages CDN url for this picture. |
| `Mixes` | Bitmask of `1 << (int)MixEnum` — XX = 1, Phoenix = 2, Phoenix 2 = 4. Which mixes render *this* picture; an avatar's availability is the union across its group. |
| `SortOrder` | Alphabetical by name, assigned at seed time. |

Seeded by migration and otherwise static. There is no scheduled refresh: all three source pages
need a login, and the dedupe is a pixel comparison that has no business running at request time.
A future mix means re-running the recon probe and adding rows.

The pin itself is two columns on `scores.User`:

- `ImportedProfileImage` — nullable; what the last import saw, whether or not it is being shown.
- `AvatarIsPinned` — the flag.

`ProfileImage` keeps its existing meaning: **the avatar to show**. That is deliberate — 193 files
read it, and none of them change.

### 4.1 The fragile seam

`UpdateUserHandler` and `SetUserContentLockHandler` rebuild the whole `User` record by hand,
carrying each field forward individually (`existing?.GameTag`, `existing?.ProfileImage`, …). Add
two fields and forget one, and **saving your country silently unpins your avatar**. Same for the
six `new User(...)` projections in `EFUserRepository`. This is covered by a test rather than by
care.

## 5. What a pin may point at

`PinAvatarCommand` takes a url and rejects anything that is not an absolute https url under
`https://piuimages.arroweclip.se/avatars/`. A player must not be able to point their avatar at an
arbitrary image on the internet.

The guard is a prefix check rather than a catalog lookup on purpose: it keeps Identity from
needing a project reference to Catalog, and a url that is on our own avatar CDN but not in the
catalog is still an avatar, still ours, and harmless.

## 6. The picker

A dialog, not a page. Picking an avatar is a pick-and-close task, and a 170-tile grid inline would
swallow the Profile form.

- **One list**, not one section per mix — the catalog is one set of avatars, so mix sections would
  show most of them three times.
- Tiles render the stored picture with a **Now** badge on the one currently worn, and a **2** badge
  on the twelve that have a second picture.
- The panel above the grid shows the selection large, its name, and **Available in** chips for the
  three mixes, greyed where the avatar does not appear. Those chips are information, not a control.
- The twelve multi-picture avatars get a small two-thumbnail switcher in that panel.
- Search filters by name. There is no global mix filter (owner call) — with one deduped list there
  is little left for it to do.

The grid lazy-loads, so a typical search-and-pick touches a couple of dozen images rather than all
170.

## 7. Provenance

The catalog was built by `ScoreTracker.ExplorationTests/LiveSite/AvatarCatalogReconTests` — a
manual-only, config-gated, read-only probe that fetches the three pages and dumps them for offline
parsing. It is the reproducible path back to this data when a new mix lands, and it is the reason
the numbers above can be re-derived rather than trusted.
