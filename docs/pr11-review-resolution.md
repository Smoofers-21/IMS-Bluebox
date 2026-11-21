# PR #11 public demo review resolution

The owner supplied `pr11-review.md`, a static review of commit `a5f62d0`, and requested useful improvements on PR #11. The public STAFF demo, existing copy/paste login, five-table schema, and free Azure hosting remain the agreed scope. No DDL or live database change is required.

| Finding | Decision |
| --- | --- |
| 1 — repeated writes and page refreshes | Add 20 stock-movement POSTs per minute per client IP and a separate 300-application-request limit per minute per IP. Keep the existing 10-login-POST limit. Rate limiting runs before cookie/database validation, excludes served static files from the application-request limit, returns 429 and Retry-After, and uses the existing trusted proxy handling. IP partitions avoid sharing one quota across every visitor using the public account; visitors behind the same IP still share a quota. These in-memory, per-instance limits reset on restart and do not guarantee availability or a monthly usage cap. Document free-allowance checks without adding paid monitoring or changing Azure settings. |
| 2 — hard-coded display credentials | Read PublicDemo:Username and PublicDemo:Password from configuration. Render the panel and its script only when both are nonblank; empty defaults keep other deployments hidden. Values are Razor-encoded and display-only. Document Azure settings, account-password synchronization, hiding the panel versus revoking access, and password rotation without a code deployment. Keep the intentionally public README credentials as the owner explicitly requested; update the README through a PR when rotating and direct visitors to the live page for the current values. |
| 3 — guessable manager username | Retain ordinary authentication and visible audit names. Recommend a unique private manager password of at least 20 random characters. Do not introduce a username-based lockout that could let anonymous requests lock the owner out. No manager account was accessed or changed here. |
| 4 — shared free-text notes | Retain the existing movement-note workflow. Notes are already HTML-encoded, and removing or hiding them changes the agreed shared stock/audit behavior. Document that notes are public, must use fictional content, and should be reviewed before presenting. No moderation, deletion, or silent alteration of history is added. |
| 5 — small optional changes | Keep the requested copy/paste flow; omit autofill. Print SDK/Docker versions in the local helper and document forwarding dotnet test arguments. Correct the validation record with the verified original CI result. |

## Verification

The original reviewed commit `a5f62d0` passed [run 44875407099](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44875407099): both jobs succeeded, including **107 SQL-enabled suite tests, 0 failures**. Its public run page, commit, and SQL result summary were checked. That result does not establish the later review changes' outcome.

For these changes, locked restore and Release tests passed **56 tests, 40 SQL groups skipped, 0 failures**. Coverage verifies configured/absent/partial display settings, HTML encoding, the normal login/CSRF/security headers, movement and application-request limits, independent trusted client addresses, spoof rejection, static assets, and GET access after exhausting movement submissions. A new SQL regression sends 21 valid movement submissions and requires exactly 20 history rows and quantity changes; it runs against a generated disposable database only.

The local SQL helper was attempted again, but Microsoft's image download returned 403 before SQL Server could start. No local SQL execution is claimed and no Azure database was used. The added SQL case is included in the existing PR CI workflow; check the latest head's successful SQL job before merging.

Chromium checks passed the configured panel's native disclosure, keyboard access, actual clipboard copy/paste, denied/unavailable clipboard fallback, no-JavaScript access, CSP, no automatic login POST, and responsive layouts at 320/375/768/1440 pixels. Separate checks confirmed that an unconfigured deployment contains neither the panel nor its script while normal login remains visible at those widths. Script syntax, shell syntax, documentation links, and the regenerated Word plan were checked.

## Owner setup after merge

Add PublicDemo__Username and PublicDemo__Password in App Service using the [documented public values](azure-portfolio-setup.md#public-demo-display-settings). Apply/Save, then manually deploy the reviewed main after its CI succeeds. Keep the public account ACTIVE/STAFF and its actual password synchronized with the displayed password. No schema, data seed, account reset, or automatic deployment is part of this PR. Live launch remains pending.
