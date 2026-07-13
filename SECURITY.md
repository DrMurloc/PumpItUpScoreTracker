# Security

## Reporting a vulnerability

Please report suspected security issues **privately** — open a GitHub security advisory on this repository (Security → Advisories → *Report a vulnerability*) rather than a public issue or PR. We'll acknowledge and work a fix before any public disclosure.

## Remembered PIUGAME credentials

The site can optionally remember your piugame.com login **on a single device** so score imports are one tap ("Remember my password"). This is the one place we retain a user credential, so it is handled with a strict, auditable model.

### Invariants (never violated)

- **No password — plaintext *or* encrypted — is ever written to our database or to a log.** The database stores only a *wrapped encryption key*, never the credential itself. The username lives only inside the encrypted blob, never as plaintext and never in the database.
- **Decryption requires three independent trust boundaries, together:** the ciphertext in **your browser** (local storage), a wrapped data-key in our **SQL database**, and a master key in **Azure Key Vault**. Any one — or any two — is useless on its own.
- **The password never travels our message bus.** It is exchanged for a short-lived piugame session id on the request thread and immediately discarded; background import work carries only that session id.

### How it works (envelope encryption)

1. A random 256-bit **data key (DEK)** is generated per stored credential.
2. Your `{username, password}` is sealed with **AES-256-GCM** under that DEK. The authenticated-associated-data binds the blob to its database row and your account, so a blob cannot be replayed onto another account.
3. The DEK is **wrapped** by a master key that **never leaves Azure Key Vault** (the vault performs the wrap/unwrap; the key material is never exported to the app).
4. The **wrapped DEK** is stored in SQL (`scores.UserImportCredentialKey`). The **AES-GCM ciphertext** is stored in your browser's local storage. The **master key** stays in Key Vault.

To import, the three pieces are brought together for the moment of use: the wrapped DEK is unwrapped by Key Vault, the ciphertext is decrypted, the credential is exchanged with piugame for a session id, and the plaintext is discarded. It is held only in a redacting in-memory type in between, never logged.

### What this does *not* protect against

- **This is not zero-knowledge.** Because the login must run on our server, we are technically able to decrypt your credential at the moment you trigger an import. We choose not to log or retain it and the code is built to make retention impossible — but you are trusting us with the same access we already have on every manual import.
- Simultaneous compromise of **both** our server process **and** Key Vault, or a cross-site-scripting flaw on our own origin (which would compromise any login, remembered or not).

Because a copy of your credential exists at rest (encrypted, on your device), **use a piugame password that is unique to piugame** — one that does not also open your email, bank, or anything else.

### Revocation & incident response

- **Forget on this device** clears the browser's copy. **Forget everywhere** deletes your key rows, so every device's copy becomes permanently un-decryptable (it self-heals to manual entry on next import).
- **Suspected breach:** an admin action deletes **all** wrapped-key rows and advances a global generation — every remembered credential everywhere is instantly bricked, and each browser purges its now-dead copy on next load. Rotating the Key Vault master key is an additional manual step; the application identity holds only wrap/unwrap rights, never key-management rights.

Design detail: [docs/design/import-widget-remember-password.md](docs/design/import-widget-remember-password.md).

## Other handling

- Credentials in flight (the existing PIUGAME login and score import) ride a redacting `RedactedString` type whose `ToString` masks the value; an architecture test (`CredentialHygieneTests`) enforces that password-bearing messages use it.
- The application authenticates to Azure (Key Vault, and — in progress — SQL) with its **managed identity**, not stored secrets.
