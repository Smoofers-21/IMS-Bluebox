# Requirements and acceptance criteria

**Status:** both milestones’ application features and PR #5 review fixes are merged into main at `13b4f43`. The [main-review resolution](main-review-resolution.md) documents this follow-up without DDL changes. Use the [validation record](validation.md) and current revision’s CI; Windows/teacher-server and live OMDb checks remain separate.

## Permissions

| Action | Staff | Manager |
| --- | --- | --- |
| View catalog, inventory, overview, and activity | Yes | Yes |
| Record ordinary check-in/check-out | Yes | Yes |
| Add/edit/archive catalog items | No | Yes |
| Correct a mistaken transaction | No | Yes |
| Manage employees and locations | No | Yes |
| Edit quantities directly or modify/delete history | No | No |

Permissions must be enforced by the server, not only by hiding buttons.

## Business requirements

| ID | Requirement | Acceptance criterion |
| --- | --- | --- |
| AUTH-01 | Authenticate employees | Correct credentials for an active account allow access; invalid credentials do not. |
| AUTH-02 | Respect employment status | Inactive and terminated employees cannot sign in or continue authorized operations. |
| AUTH-03 | Enforce roles | A staff account cannot perform manager operations through a page or direct request. |
| AUTH-04 | Keep account setup simple | Managers create accounts; there is no public registration or forced initial password change. |
| AUTH-05 | Preserve manager access | Deactivating or demoting the last active manager is rejected, including competing account edits. |
| AUTH-06 | Preserve password resets | A blank edit password retains the existing hash; a stale login-time hash upgrade cannot overwrite a manager’s password reset. Password/username changes invalidate existing sessions on their next request, while login rehashing issues a valid new session. |
| AUTH-07 | Bound sign-in attempts | Each client address can send 10 login POSTs per minute per application instance; subsequent POSTs return 429 with retry guidance. GETs and other routes remain available. Unknown usernames still undergo hash verification; session cookies require HTTPS. |
| CAT-01 | Maintain a movie/game catalog | Managers can create and edit required name, type, and format fields; blank required fields and invalid types are rejected. |
| CAT-02 | Distinguish formats | The same title in DVD and Blu-ray editions has separate entries/balances. After the first movement, the item’s type and format cannot change, even when its stock returns to zero; other editions require a new entry. |
| CAT-03 | Keep metadata optional | Items can be saved without a poster or age rating. |
| CAT-04 | Use OMDb for movies | A manager selects a movie match and can review its returned poster and age classification before saving. |
| CAT-05 | Handle missing external data | Failed lookup, no match, or unavailable fields leave manual item entry usable. Games use manual metadata. |
| INV-01 | Maintain location balances | Each item/location pair has at most one balance. New balances start at zero; creating an item adds no units. |
| INV-02 | Check stock in | With 5 on hand, checking in 10 results in 15 and one new movement entry. |
| INV-03 | Check stock out | With 15 on hand, checking out 4 results in 11 and one new movement entry. |
| INV-04 | Validate movement quantities | Zero, negative, fractional, missing, and out-of-range quantities are rejected without changing inventory or history. Missing movement direction is rejected too. |
| INV-05 | Prevent negative balances | Checking out more than available is rejected. Removing exactly the available quantity results in zero. |
| INV-06 | Save reliably | Balance update and history insert both commit, or neither persists. |
| INV-07 | Handle competing updates | If two check-outs together exceed available stock, they cannot both succeed and create a negative or inconsistent balance. |
| INV-08 | Validate the final balance | The server rechecks stock when confirming a movement, regardless of the earlier screen preview. |
| INV-09 | Use active records | Movements against archived items or inactive warehouses are rejected. |
| HIST-01 | Attribute movements | Each entry stores the signed-in employee, item, location, direction, positive quantity, and server-generated UTC time. |
| HIST-02 | Retain history | Application users cannot edit/delete historical movement entries or erase them through retirement; item type/format cannot be relabeled after history exists. |
| COR-01 | Restrict corrections | Only managers can submit a correction; an explanation and original-transaction reference are required. |
| COR-02 | Preserve the original | A correction adds an opposite movement for the original item/location; the original remains unchanged. Corrections cannot bypass stock validation. |
| RET-01 | Retire items safely | An item with stock at any warehouse cannot be archived. Archived items remain identifiable in history. |
| RET-02 | Retire locations safely | A warehouse with stock cannot be deactivated. Its historical movements remain visible. |
| VIEW-01 | Find stock | Inventory can be searched by item name/ID and filtered by location and item type, following the mockups. Catalog, Inventory, and Manage Catalog expose matches through 100-row pages; Previous/Next preserve filters and a new filter submission starts at page 1. |
| VIEW-02 | Review activity | Users can inspect employee-attributed movements and filter by item, movement type, and location; older results remain accessible through 100-row pages. |
| VIEW-03 | Summarize inventory | Total units equal the sum of the returned warehouse totals. The READ COMMITTED batch also returns counts and five recent movements; those sections may reflect slightly later commits and do not hold serializable dashboard range locks. |

Correction policy: each movement may have one full opposite-movement correction. A correction can itself be reversed once, producing a chain rather than multiple children for the same movement. Partial reversals are not supported. The server derives the item, warehouse, direction, and quantity from the selected entry; the manager supplies an explanation. Inactive/archived records must be reactivated by a manager before correction, and insufficient stock blocks reversal. All original entries remain unchanged.

## Data and security expectations

- Use fake demonstration records, not real employee personal data.
- Store password hashes using a supported library; do not create a custom hashing scheme.
- Keep database credentials and the OMDb API key outside tracked files and browser responses.
- Validate input on the server and use parameterized ADO.NET queries.
- Use database constraints alongside application validation.
- Store timestamps in UTC. Activity and overview display labeled Central time (CST/CDT), including daylight-saving transitions. If neither Central time-zone ID is available, show clearly labeled UTC.

## Validation approach

Focused integration tests now exercise sign-in, permissions, stock updates, rollback, competing check-outs, and the HTTP workflow. Milestone 2 adds tests for manager HTTP authorization, last-manager protection, retirement, correction chains/rollback/concurrency, dashboard totals, and OMDb fallback using a fake HTTP provider. A configured live OMDb account still requires a manual smoke check. See the [development guide](development.md#automated-checks) for the distinction between local checks and SQL Server integration validation.

See the [demo guide](demo-guide.md) for the presentation sequence.

Text columns follow the supplied DDL: most are VARCHAR and use their database/column code page, while poster URLs use NVARCHAR. Full Unicode names/notes are not guaranteed by a non-Unicode code page. This revision does not assume Windows-1252, impose ASCII-only inputs, or change the schema.
