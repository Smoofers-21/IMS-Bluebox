# External review resolution — consolidated PR #5

**Historical record:** PR #5 was merged into main at `13b4f43`. The merge instructions below describe that completed consolidation. Later suggestions are tracked in the [main-review resolution](main-review-resolution.md).

The owner supplied `pr4-review-findings.md` as review input and requested that useful, schema-compatible fixes go into PR #5. PR #5 already contains all PR #4 commits and the documentation refresh. It is now the consolidated application/documentation/fix PR: **merge PR #5 directly after its latest checks pass; PR #4 does not need to be merged first.**

The review suggestions were checked against the implementation and supplied five-table DDL. No tables, columns, indexes, collation, or database isolation settings were changed. The instructor's Program/Main/MapRoutes and ADO.NET foundation is retained.

## Decisions

| Finding | Resolution |
| --- | --- |
| 1. Overlapping PRs | PR #5 intentionally supersedes PR #4 for the owner's requested single merge. It includes Milestone 2, refreshed documentation, and the fixes below. |
| 2. Sessions survive credential changes | Cookies include a SHA-256 credential stamp derived from the existing username/hash fields. Password resets and username changes invalidate old sessions on their next request. Login-time rehashing issues the new cookie with the upgraded hash. No new security-stamp column is needed. |
| 3. Retirement lock-order cycle | Item/warehouse edits take the parent exclusive lock before checking inventory. Existing stock movements finish first; later movements wait before taking child stock locks. The new SQL tests deliberately pause a movement and check that retirement waits at the parent without deadlock retries. |
| 4. Overview uses global serializable locks | The dashboard uses a READ COMMITTED batch. Total units are calculated from the returned warehouse totals, so those values agree without a serializable dashboard transaction. Counts/recent entries may reflect slightly later commits; stock writes remain atomic and serializable. |
| 5. Type/format changes reinterpret history | Type and format cannot change after an item's first movement, even if stock is later zero. Create a new item for another edition. Name, optional metadata, and safe status changes remain editable. The form reflects the restriction and the service enforces it. |
| 6. Self-account changes are confusing | The form warns that saving one's own account signs it out. A successful self-edit explicitly signs out and redirects to Login with a message describing whether the account remains active. Last-active-manager protection still applies. |
| 7. Retired-record correction message | Correction failures explicitly say to reactivate the item or warehouse; ordinary movement selectors retain their existing active-record message. |
| 8. OMDb too-many-results error | Distinguish a broad search from no match and provider failure. Ask for a more specific title; do not suggest an unimplemented year input or expose raw provider/key errors. |
| 9. Missing Central time zone | Try the IANA ID, then the Windows ID, then UTC. The fallback is labeled UTC in both formatted values and the activity caption. |
| 10. Broad HTTPS poster policy | Retained to support agreed manual movie/game poster URLs. Only managers can save them; HTTPS validation, no-referrer image requests, and no server-side arbitrary-image fetching remain. Restricting all posters to the movie CDN would break manual game posters. |
| 11. Duplicate push/PR cancellations | Run CI on pull requests and main pushes. Cancel only superseded runs within the same PR/ref; a feature push no longer starts a duplicate run alongside its PR event. |
| 12. VARCHAR encoding limitations | Existing VARCHAR columns inherit the database/column code page. Do not assume Windows-1252, impose ASCII-only names, or silently change the teacher's schema. Demo data uses supported text; full Unicode storage requires a separately reviewed schema/encoding decision. Poster URLs already use NVARCHAR. |
| 13. Small cleanup suggestions | Remove redundant hidden record IDs and bind record IDs from the URL, with tampered-form regression coverage. Keep the established small service/page structure. Extra abstraction, duplicate-title warnings, and larger administration paging are outside this fix's class-demo scope. |
| 14. Documentation drift | Update requirements, architecture, plan/Word copy, workflow guidance, and validation notes. Treat old counts/runs as historical evidence for their named commits; use current PR checks for the new code. |

## Verification

See the [current PR #5 checks](https://github.com/Smoofers-21/IMS-Bluebox/pull/5/checks) and [validation record](validation.md) for results. The expanded regression suite covers credential revocation, legacy-hash login, unchanged-credential sessions, edition identity, self-edit sign-out, route-ID tampering, retired corrections, OMDb error messages, time-zone fallback, and deterministic item/warehouse retirement lock ordering. The existing suite continues to cover quantities, concurrency, atomic history, correction chains, account safeguards, and dashboard totals.

SQL integration tests use generated databases in the disposable GitHub SQL Server instance, including a test-only DMV query to observe a blocked retirement. They do not run against the teacher's server. Windows/teacher-server testing, a live OMDb check, and final screenshots remain manual sign-off tasks.
