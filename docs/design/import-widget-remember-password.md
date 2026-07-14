# Import widget + Remember My Password — technical scope

Status: **built (C1–C12)** on branch `claude/import-widget-password-memory-5628e5`.

**Deferred to follow-up** (tracked here, not blockers): a nonce-based *enforced* CSP (C11 shipped the baseline headers; the inline theme `<style>` needs a per-request nonce before CSP can enforce); the localization sweep across the 8 non-English locales (new strings display in English via key-fallback until then); backgrounding the `/UploadPhoenixScores` entry point (it shares the `RunImport` body from C5 but keeps its synchronous progress flow — the widget is the backgrounded entry); the credential-generation client self-purge (the admin key-cycle already bricks every blob server-side by deleting the key rows); and removing the now-unused `_currentUser` field in `OfficialSiteClient`.

Three shipped things + one hardening pass:

1. **Import Scores widget** — a 1×1 home-dashboard widget that imports scores (mirrors `quick-record`).
2. **Remember My Password** — an encrypted, **device-only** credential store (envelope encryption; three trust boundaries).
3. **Background-job import** — the credentialed scrape moves off the Blazor circuit; the password is exchanged for a piugame session id (`sid`) on the circuit, and only the `sid` crosses to the background job.
4. **Hardening** — CSP + security headers, `SECURITY.md`, and the credential-hygiene invariant.

The mock (owner-approved through v4) is the UX contract: https://claude.ai/code/artifact/2e27a456-096d-459d-84af-4c229b1389de

---

## 0. Invariants (non-negotiable — go in SECURITY.md)

- **No password — plaintext or encrypted — is ever written to the database or a log.** The DB holds only a *wrapped key*. The username lives only inside the encrypted blob (never plaintext, never DB).
- **Three trust boundaries, all required to decrypt:** ciphertext in the **browser** (local storage), wrapped data-key in **SQL**, master key in **Azure Key Vault**. Any one — or any two — is useless.
- **The widget never stores.** Credential capture happens only in (a) the configurator's Store form and (b) the `/UploadPhoenixScores` "remember" checkbox. The widget imports (typed one-time, or from a stored credential) but never runs the storage dance.
- **The password never rides the bus.** It is exchanged for a `sid` on the circuit and discarded; background messages carry only the `sid`.

---

## 1. Crypto design (envelope encryption)

Per stored credential:

1. Generate a random 256-bit **DEK**.
2. `AES-256-GCM` encrypt the UTF-8 JSON `{ "u": username, "p": password }` under the DEK. Random 96-bit nonce. **Associated data = `keyId ‖ userId ‖ version`** (binds the blob to its DB row + account; blocks cross-account replay and downgrade).
3. **Wrap** the DEK with the Key Vault master key (KV `wrapKey`/`unwrapKey`, RSA-OAEP). The master key never leaves the vault.
4. Persist `{ keyId, userId, wrappedDek, createdAt }` in SQL (`UserImportCredentialKey`).
5. Hand the client `{ keyId, ciphertext }` (blob = `version(1) ‖ nonce(12) ‖ tag(16) ‖ ciphertext`, base64) → local storage.

Decrypt reverses it: load `wrappedDek` by `(keyId, userId)` → KV unwrap → DEK → AES-GCM decrypt with the same AAD. Missing row, wrong user, or auth-tag failure → `CredentialUnlockException` → the UI falls back to typed entry.

**Revocation.** *Forget here* = clear local storage. *Forget everywhere* = delete the user's `UserImportCredentialKey` rows (every device's blob is now un-decryptable, self-heals to typed entry). **Breach** = admin "Cycle all keys": delete **all** rows + bump a global generation — deleting the rows alone bricks every blob (no wrapped DEK → no decrypt). Rotating the KV master key is an **optional manual portal step** (belt-and-suspenders), so the app's identity needs only wrap/unwrap, never key-management rights.

`AES-GCM` is `System.Security.Cryptography.AesGcm` (BCL — no package). KV wrap/unwrap is the only new external dependency.

---

## 2. Where each piece lives (layering)

| Concern | Home | Notes |
|---|---|---|
| KV wrap/unwrap master-key ops | `Domain.SecondaryPorts.IKeyEnvelope` + impl `Data/Clients/KeyEnvelope` | Azure SDK is allowed in `Data` (Blob already lives there). One impl: KV when configured, config-provided local key otherwise (dev/E2E). |
| Wrapped-DEK storage | Identity vertical (`UserImportCredentialKey` entity + `IImportCredentialKeyStore`) | Per-user key material = accounts concern. Internal; `Set<TEntity>()`. |
| Envelope orchestration (protect/unprotect/forget/cycle) | Identity `IImportCredentialProtector` (impl in `Identity/Infrastructure`) | Uses `IKeyEnvelope` + `IImportCredentialKeyStore` + `AesGcm`. No new package in Identity. |
| Credential MediatR surface | Identity `Contracts/` | Store / Reveal / Forget / ForgetAll / CycleAllKeys. |
| Session-id mint + import orchestration | OfficialMirror | `SignIn` (mint sid), `StartOfficialImportCommand` (circuit), `RunOfficialImportCommand` (bus) + consumer, shared import body. |
| Client-side blob storage | Web `IImportCredentialClientStore` + `wwwroot/js/credential-storage.js` | Raw `localStorage` (NOT `ProtectedLocalStorage` — its DP ring rotates/isn't persisted and is shared with auth cookies). |
| Widget + configurator + disclaimer + upload-page changes | Web | `Components/HomeWidgets/`, `Pages/UploadPhoenixScores.razor`, a shared disclaimer dialog. |
| Admin cycle button | Web `Pages/Admin/` | Dispatches `CycleAllImportCredentialKeysCommand`. |
| CSP/headers | Web `Program.cs` middleware + `_Host` nonce | Report-only → enforce. |

---

## 3. The three flows

### A. Store a credential (configurator Store button, or Upload-page "remember" + Import)
Circuit → `StoreImportCredentialCommand(username, password)` → Identity handler → `IImportCredentialProtector.Protect(userId, u, p)` → `{keyId, ciphertext}` → circuit writes blob to local storage via `IImportCredentialClientStore`. First time ever: the `RememberPasswordDisclaimerDialog` must be accepted first (versioned consent recorded in UiSettings).

### B. Import from a stored credential (widget "Saved credentials loaded", or Upload-page saved variant)
Circuit reads `{keyId, ciphertext}` from local storage → `StartOfficialImportCommand(Stored(keyId, ciphertext), mix, cardId?, includeBroken, syncPiuTracker)` → **OfficialMirror handler** (runs on circuit): `RevealImportCredentialQuery(keyId, ciphertext)` → Identity → `{username,password}` → `IOfficialSiteClient.SignIn(mix, u, p)` → `sid` → **discard password** → `IBus.Publish(RunOfficialImportCommand(userId, mix, sid, cardId, includeBroken, syncPiuTracker))` → return. The consumer runs the scrape off-circuit; progress rides the existing `ImportStatusUpdatedEvent` static-event bridge in `MainLayout`.

Unlock fails (`CredentialUnlockException`) → the widget shows the typed-entry state with the title-bar warning icon + tooltip.

### C. Typed one-time import (widget with no stored credential; Upload page unchecked)
`StartOfficialImportCommand(Typed(username, password), mix, …)` → OfficialMirror handler → `SignIn` → `sid` → publish `RunOfficialImportCommand`. Nothing stored.

**Card/gametag step.** Skip-gametag ON → reuse the stored `PhoenixScoreUpload__LastGameId` (UiSettings), no card fetch. OFF or no stored card → after `SignIn`, fetch `IOfficialSiteClient.GetGameCards(mix, sid)` on the circuit, show the picker, then publish with the chosen `cardId`.

**Where the sid mint happens:** on the circuit (fast; ~1 network call), so `InvalidCredentialException` surfaces inline exactly like today. Only the multi-minute scrape is backgrounded.

---

## 4. Layer-by-layer changes

### SharedKernel / Domain
- Reuse `RedactedString` for all credential + `sid` message properties.
- `Domain.SecondaryPorts.IKeyEnvelope`: `Task<byte[]> Wrap(byte[] dek, ct)`, `Task<byte[]> Unwrap(byte[] wrapped, ct)`, `Task CycleKey(ct)`.
- New domain exception `CredentialUnlockException` (Identity domain) for decrypt failures.

### Identity vertical
- **Entity** `UserImportCredentialKey` (internal, `Infrastructure/Entities/`): `KeyId (Guid, PK)`, `UserId (Guid, indexed)`, `WrappedDek (varbinary)`, `CreatedAt (datetimeoffset)`. Registered in Identity's `IDbModelContribution`. **New migration.** Row in `DATABASE-SCHEMA.md`.
- **Port + store** `IImportCredentialKeyStore` + `EFImportCredentialKeyStore` (`Set<UserImportCredentialKey>()`): `Save`, `Get(keyId, userId)`, `DeleteForUser(userId)`, `Delete(keyId, userId)`, `DeleteAll`.
- **Protector** `IImportCredentialProtector` + impl: `Protect(userId,u,p) → {keyId,ciphertext}`, `Unprotect(userId,keyId,ciphertext) → {u,p}`, `Forget(userId,keyId)`, `ForgetAll(userId)`, `CycleAllKeys()`. Uses `IKeyEnvelope` + `IImportCredentialKeyStore` + `AesGcm`.
- **Contracts** (`Contracts/Commands|Queries`):
  - `StoreImportCredentialCommand(RedactedString Username, RedactedString Password) : IRequest<StoredImportCredential>`; `StoredImportCredential(Guid KeyId, string Ciphertext)`.
  - `RevealImportCredentialQuery(Guid KeyId, string Ciphertext) : IQuery<RevealedImportCredential>`; `RevealedImportCredential(RedactedString Username, RedactedString Password)`.
  - `ForgetImportCredentialCommand(Guid KeyId) : IRequest`, `ForgetAllImportCredentialsCommand : IRequest`.
  - `CycleAllImportCredentialKeysCommand : IRequest` (admin) — deletes all key rows + bumps the global generation (no KV call; master-key rotation is a manual portal step).
  - `GetImportCredentialGenerationQuery : IQuery<int>` (for the client blob self-purge).
- **Handlers** in `Application/`, using `_currentUser` for userId. `[ExcludeFromCodeCoverage]` on the records per convention.

### OfficialMirror vertical
- **`IOfficialSiteClient` session refactor.** Import-path methods take a `sid` instead of `username,password` (they reconstruct an `HttpClient` via a new `IPiuGameApi.ClientForSid(mix, sid)` — no network): `GetAccountData(mix, sid, id, ct)`, `GetScorePageCount(mix, sid, ct)`, `GetRecordedScores(mix, userId, sid, id, includeBroken, limit, ct)`, `GetGameCards(mix, sid, ct)`. New `Task<string> SignIn(mix, username, password, ct)` (wraps `GetSessionId`, returns the sid). Collapses today's ~4 logins/import to 1. Login-only methods (`GetAccountIdentity`) stay credential-based.
- **Shared import body.** Extract the current `OfficialLeaderboardSaga.Handle(ImportOfficialPlayerScoresCommand)` body into an internal `RunImport(userId, mix, sid, cardId, includeBroken, syncPiuTracker, ct)` — takes explicit `userId` (drops `_currentUser`), uses the sid-based client methods.
- **New messages** (`Contracts/`):
  - `StartOfficialImportCommand(ImportCredentialSource Source, MixEnum Mix, string? CardId, bool IncludeBroken, bool SyncPiuTracker) : IRequest` — `ImportCredentialSource` = abstract record with `TypedCredentialSource(RedactedString Username, RedactedString Password)` / `StoredCredentialSource(Guid KeyId, string Ciphertext)`.
  - `RunOfficialImportCommand(Guid UserId, MixEnum Mix, RedactedString Sid, string CardId, bool IncludeBroken, bool SyncPiuTracker)` — plain bus record (`Application/Messages`-style, in `Contracts/Messages`).
- **`StartOfficialImportCommand` handler** (circuit): resolve creds (Typed → direct; Stored → `RevealImportCredentialQuery`) → `SignIn` → publish `RunOfficialImportCommand`. Surfaces `InvalidCredentialException` synchronously.
- **`RunOfficialImportConsumer : IConsumer<RunOfficialImportCommand>`**: calls `RunImport(...)`. Registered via the existing `AddOfficialMirrorConsumers(IRegistrationConfigurator)` hook.
- **API path preserved.** `PhoenixScoresController` + `ImportOfficialPlayerScoresCommand` keep their **synchronous, credential-based external contract** (contract tests pin this) — the handler becomes a thin adapter: `SignIn` → `RunImport(...)` synchronously. No wire-shape change.
- The old circuit dispatch of `ImportOfficialPlayerScoresCommand` from `UploadPhoenixScores.razor` is replaced by `StartOfficialImportCommand`.

### Data / Infrastructure
- **Key Vault client.** `Data/Clients/KeyEnvelope : IKeyEnvelope`. Packages (allowlist update in CLAUDE.md, Data row): `Azure.Security.KeyVault.Keys`, `Azure.Identity`. Config section `KeyVault { VaultUri, KeyName }`; auth = `DefaultAzureCredential` (managed identity in prod). When `KeyVault` is unconfigured (dev/E2E/tests), the same impl uses a config-provided symmetric wrap key (`KeyVault:LocalKey`) — one impl, config branch, so "one implementation per port" holds.
- EF migration for `UserImportCredentialKey`.

### Web / Presentation
- **Local-storage interop.** `wwwroot/js/credential-storage.js` (`get/set/remove`), `Services/IImportCredentialClientStore` (+ impl) exposing `Read()/Write(blob)/Clear()` where blob = `{ keyId, ciphertext, generation }`. Game tag for display comes from the existing UiSettings `GameTag`.
- **Widget.** `Components/HomeWidgets/ImportScoresWidget.razor` + `ImportScoresConfigPanel.razor` + `ImportScoresConfig { string mix-mode fields like QuickRecord, bool SkipGameTag = true }`. Registry descriptor `"import-scores"`, `SupportedSizes: [OneByOne]`, all mixes, category `Play` (or `Utility`). Widget states per mock; uses `HeaderSlot` for the mix chip and the unlock-fail warning icon. Config Store/Forget actions are **immediate** (touch local storage + DB), separate from the panel Save (which persists mix-mode + skip-gametag).
- **Disclaimer.** Shared `RememberPasswordDisclaimerDialog` (MudDialog) — layman body + three-key diagram + password-reuse warning + a "technically curious" expander (guru copy). Used by both the configurator and the upload page. **Versioned consent** in UiSettings (`RememberPasswordConsent = <version>`); re-prompt only on a material reword.
- **Upload page.** Rewrite the password-import section onto `StartOfficialImportCommand`; add the remember checkbox (→ disclaimer → Store on import) + the saved-credentials variant; **un-gate Phoenix 2** (drop `IsImportGated`). CSV path unchanged.
- **Admin.** A "Cycle all credential keys" button (confirm dialog) → `CycleAllImportCredentialKeysCommand`.
- **CSP + headers.** Middleware in `Program.cs`: `Content-Security-Policy` (script-src 'self' + nonce; style-src 'self' + nonce — the inline `MixThemes` `<style>` needs the nonce), `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `frame-ancestors 'none'`. **Report-only first**, then enforce. Verify anti-forgery on the `/Login/PiuGame` POST; verify `ArcSvg()` interpolates no user strings.

### CompositionRoot
- `IKeyEnvelope` auto-binds (impl in `Data`). Identity's `AddIdentity()` registers `IImportCredentialProtector` + `IImportCredentialKeyStore`. `KeyVault` options binding. The new OfficialMirror consumer via its existing consumer hook.

---

## 5. Session-clearing on breach (interpretation — confirm)

We do **not** hold a piugame `sid` across page loads (each import mints a fresh one), so there is no long-lived server session to clear. "Cycle keys → clear sessions on next page load" is therefore implemented as a **client-side blob self-purge**: the cycle bumps a global generation; the stored blob carries the generation it was written under; on page load the app compares (via `GetImportCredentialGenerationQuery` / a claim) and, if stale, deletes the local blob so users aren't stuck presenting a dead credential. Server-side the key rows are already gone, so decrypt would fail regardless. If you meant something heavier (force app re-auth), say so.

---

## 6. Tests
- **DomainTests:** protector round-trip; unlock-fail on missing row / wrong user; AAD cross-account rejection; cycle invalidates. (Fake `IKeyEnvelope` = local key.)
- **ApplicationTests:** `Store`/`Reveal`/`ForgetAll`/`Cycle` handlers; `StartOfficialImportCommand` publishes `RunOfficialImportCommand` with a sid and **no password** (`IBus.Verify`); typed vs stored branches.
- **Tests.Components (bUnit):** widget states (saved / typed / unlock-fail / mix-picker / csv); configurator Store→chip→Forget; disclaimer gate. New widget satisfies `WidgetRenderContractTests` (5-param contract). Regenerate the capability-schema golden.
- **Tests.Api:** import contract unchanged (shared-body extraction must not shift the wire shape).
- **Tests.Integration:** `EFImportCredentialKeyStore` + migration against real SQL; protector round-trip with the local-key envelope.
- **Tests.E2E (optional, critical path):** store → import-from-saved happy path against the WireMock piugame stub.
- `CredentialHygieneTests` stays green (new `*Password*` props are `RedactedString`); optionally extend it to also catch `*Sid*`/`*Session*`.

## 7. Docs & localization
- **New:** `SECURITY.md` (invariants + threat model + incident response), this design doc.
- **Update:** `DATABASE-SCHEMA.md` (new table), `docs/design/home-page-widgets.md` (widget entry), `CLAUDE.md` (Data package allowlist), `API.md` (note: import contract unchanged).
- **Localization:** every new string through `L[…]` in all 9 locales — the disclaimer is a large copy surface (ja/ko best-effort, flagged for volunteer review).

## 8. Commit plan
- **C1** Docs-first: `SECURITY.md` + this doc + `DATABASE-SCHEMA` row + `CLAUDE.md` allowlist.
- **C2** `IKeyEnvelope` + `Data/Clients/KeyEnvelope` (KV + local-key fallback) + config + packages + wiring. Unit tests (local key).
- **C3** Identity `UserImportCredentialKey` entity + model contribution + migration + `IImportCredentialKeyStore`. Integration test.
- **C4** Identity `IImportCredentialProtector` + contracts + handlers. Domain + Application tests.
- **C5** OfficialMirror sid refactor (`SignIn`/`ClientForSid`, sid-based methods) + extract `RunImport`; keep API path synchronous. Suites green.
- **C6** OfficialMirror `RunOfficialImportCommand` + consumer + `StartOfficialImportCommand` (mint sid → publish). ApplicationTests (sid, no password).
- **C7** Web local-storage interop + shared disclaimer dialog + consent + l10n keys.
- **C8** `ImportScoresWidget` + config panel + registry descriptor + golden regen + render-contract test. bUnit tests.
- **C9** `/UploadPhoenixScores` onto the shared path + remember checkbox + saved variant + un-gate P2.
- **C10** Admin cycle-keys button + command + generation/self-purge mechanism.
- **C11** CSP + security headers (report-only) + inline-style nonce + anti-forgery check + ArcSvg verify.
- **C12** Localization sweep (×9) + docs finalize.

## 9. Open risks / owner actions
1. **Azure setup (owner/ops).** Prod needs a Key Vault + one wrap/unwrap key + the App Service **managed identity** granted **wrap/unwrap only** (*Key Vault Crypto User* on that key; or Wrap/Unwrap in access-policy mode). I can't touch Azure (read-only). Provide `KeyVault:VaultUri` + `KeyName`. **Network posture is your call and doesn't affect the build:** identity/RBAC is the real gate, so public-access-ON + RBAC-locked is already secure; disabling public access requires VNet integration + a private endpoint + the `privatelink.vaultcore.azure.net` DNS zone (the "trusted Microsoft services" bypass does **not** cover app runtime calls). Until the vault is wired, prod runs on the local-key fallback (feature behind config).
2. **Session-clearing semantics** — confirm §5 (client blob self-purge) matches intent.
3. **API import path** stays synchronous + credential-based (unchanged contract) — confirm we are not changing partner-tool behavior.
4. **CSP nonce in Blazor Server** is the fiddliest bit (the inline `MixThemes` style block); staged report-only mitigates risk.
5. **Localization volume** for the disclaimer (×9).
