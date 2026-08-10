# Culture resolution

What decides the language a page renders in, and which store is allowed to answer.

## 1. The order

```
1. ?culture= / ?ui-culture=      QueryStringRequestCultureProvider     (stock)
2. the signed-in player's saved setting   UserSettingRequestCultureProvider
3. .AspNetCore.Culture cookie    CookieRequestCultureProvider          (stock)
4. Accept-Language, exact match  AcceptLanguageHeaderRequestCultureProvider (stock)
5. Accept-Language, mapped down  CustomRequestCultureProvider → SupportedCultures.ResolveClosest
   ↓ nothing placeable
   en-US
```

A provider that returns a result containing nothing supported does not end the walk — the
middleware moves to the next one. That is what lets 5 catch what 4 could not place (`es-CL`
resolves upward to `es`, which is not a catalogue, so 4 declines and 5 maps it to `es-ES`).

## 2. What each rank is for

**1 — query string.** A one-request preview of any locale, for QC and screenshots. The most
specific intent a request can carry, so it outranks everything, including a saved setting. It is
deliberately **not durable**: nothing writes it to the cookie, so the next request is normal
again. Before that rule existed, a shared `?culture=en-ZW` link changed the recipient's language
for their whole browser session.

**2 — the saved setting.** For a signed-in player this is the answer, unconditionally. It lives
in SQL as the `Culture` key inside `scores.UserSettings.UiSettings` (a JSON blob), written by the
`/Account` language picker and by `/Setup`. It is absolute: an account set to English renders
English on a Spanish browser, forever, on every device.

**3 — the cookie.** A cache, and the only place an *anonymous* visitor can keep a choice, since
they have no row to write to. It never overrules rank 2.

**4/5 — the browser.** What a visitor gets before they have expressed any preference. Rank 5's
downward mapping is documented in `SupportedCultures.ResolveClosest`; it constructs no
`CultureInfo`, so an unplaceable tag returns null rather than throwing.

## 3. Who writes the cookie

| Where | When |
|---|---|
| `CultureController` | the picker's `/Culture/Set` navigation |
| `App.razor` | every document render — write-back, only when it differs, and **only for anonymous visitors, and never for a query-string preview** |

Both write through `CultureCookie`, which sets an explicit `MaxAge`. Nothing gives a cookie an
expiry for free here: `AddCookiePolicy` is registered but `app.UseCookiePolicy()` is never
called, so a bare `Response.Cookies.Append` writes a **session** cookie that dies when the
browser closes. That was the original bug — a signed-in player's language reverted to their
browser's on every browser restart, because the only copy that fed rendering was the one that
had just expired.

`/Logout` deletes the cookie. That is intentional: the next visitor on a shared machine gets
their own browser's language, and a returning player's setting is read from SQL anyway.

## 4. Where the setting is *not* read

The culture provider reads the setting through the same cache entry the shell uses
(`ShellModelFactory.SettingsCacheKey`, 5-minute TTL, evicted by `UiSettingSavedCacheEviction` on
every settings write), so a normal request costs no query and a language change is visible on the
very next one.

`UseRequestLocalization` runs **between `UseAuthentication` and `UseAuthorization`**. That
placement is load-bearing twice over: above `UseAuthentication` the provider cannot see who is
asking (which is why SQL had no say for years), and below `UseAuthorization` `HttpContext.User`
may have been replaced by a scheme-specific principal — so an `api/*` caller authenticating with
an `ApiToken` would start receiving its owner's language in what is meant to be stable machine
output. Between the two, `HttpContext.User` is the cookie principal or nobody.

## 5. Outside a request

Discord and email compose with no HTTP request in flight and take a different path entirely:
`ILocalizedTextAccessor` / `ResxLocalizedTextAccessor` swaps both ambient cultures around each
lookup, with the target read from SQL per recipient. `IStringLocalizer` only ever reads ambient
`CultureInfo.CurrentUICulture` — **the resx catalogues have no dependency on the cookie at all.**

## 6. Inside a circuit

A live circuit cannot change its own culture: the middleware sets it per request, and
`IStringLocalizer` resolves off the ambient value. So every language change is a real navigation
(`forceLoad`) through `/Culture/Set`, not a re-render.

## 7. Not built yet

`<Match Browser>` — a picker entry that clears the SQL key and hands the decision back to rank
4/5. Needs a clear path (`SaveUserUiSettingCommand` can only ever set a key), its own cache
eviction, and a `/Culture/Clear` action, since a circuit cannot delete a cookie.
