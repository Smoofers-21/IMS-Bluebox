# Milestone 2 review

Milestone 2 implements the remaining agreed warehouse features using the instructor's `Program.Main` / `MapRoutes` structure and parameterized ADO.NET helpers. It uses the existing five tables and supplied columns. The app requires a configured SQL Server connection and an initial account for use; it does not apply schema changes or seed data automatically. Follow the [development setup](development.md).

## Implemented features

| Feature | Entry point | Key rule |
| --- | --- | --- |
| Overview | `/Overview` | Stock uses bigint sums; all warehouse balances count, including zero-stock warehouses |
| Catalog administration | `/Admin/Items` | Movie/game and format are required; creating an item adds no stock |
| Employees | `/Admin/Employees` | New passwords use the framework hasher; blank edit password preserves the hash |
| Warehouses | `/Admin/Locations` | Name and address required; inactive warehouses cannot move stock |
| Retirement | Item/warehouse status field | Item must have zero stock everywhere; warehouse must have zero stock for every item |
| Corrections | Activity → Correct | Manager supplies an explanation; server derives all movement fields |
| OMDb | Catalog administration → Search movies | Server-side key; review the selected movie's optional metadata before saving |

Managers alone can open or submit the administration/correction pages, including lookup handlers. Transactional services recheck the actor's current manager status before writing. Account changes serialize the directory check/update so competing edits cannot remove the last active manager. Item/location edits take an exclusive parent lock before checking stock, coordinating with movements without the shared-to-exclusive parent lock-upgrade cycle. Type and format become fixed once any movement exists. Credential changes revoke old sessions on their next request; successful self-edits sign out with a clear message.

## Agreed correction policy

The owner requested corrections of corrections. Each movement may have one full reversal, including a movement that is itself a correction. Example:

| Entry | Movement | Links to | Balance from an initial 10 |
| --- | --- | --- | --- |
| A | Check out 4 | Ordinary movement | 6 |
| B | Check in 4 | Corrects A | 10 |
| C | Check out 4 | Corrects B | 6 |

A cannot be reversed again once B exists; open B to create C instead. Reversal quantity, item, warehouse, and opposite direction come from the selected entry. Each new entry records the current manager, UTC time, and required explanation. No existing history changes. Corrections require active items/warehouses and adequate stock; they cannot force negative quantities or overflow. A failed correction changes neither stock nor history.

The existing foreign key preserves references. The one-reversal-per-entry rule is enforced by application transactions locking the selected history row; the database schema alone does not enforce that application policy for direct SSMS writes.

## Review sequence

1. Run the [development setup](development.md), then sign in as staff. Confirm Overview, Catalog, Inventory, and Activity work; direct administration/correction URLs deny staff access.
2. As manager, create an employee and sign in with it. Edit the account with a blank password and verify the existing password still works. Try a duplicate username and try removing the last active manager.
3. Add a warehouse and a catalog item. Verify no stock is created. Check stock in, then attempt archival/deactivation while stocked; both should fail.
4. Remove all stock and retire the record. Verify ordinary movements and corrections against retired records fail, while existing history remains visible. Reactivate when needed.
5. Record a movement, reverse it with an explanation, and reverse that correction. Check the two-way history labels. Try repeating a reversal and reversing a receipt after stock has been used.
6. Configure OMDb privately. Select a title/year and review metadata before saving. Test missing/no-match metadata and manual game entry. Without a key, manual item entry must still work.
7. Compare total units with the displayed warehouse sums. Counts/recent movements may advance independently under READ COMMITTED; stock writes remain atomic. Times use Central/CST/CDT when available, with a clearly labeled UTC fallback.

## Validation and remaining work

Local Release build and non-SQL tests run without connecting to the teacher server. Real integration tests use generated disposable SQL Server databases in GitHub Actions. Added coverage includes staff HTTP denial, manager forms/antiforgery, account password preservation and duplicate names, competing manager changes, retirement, correction chains/concurrency/rollback, and dashboard totals. OMDb tests use a fake HTTP provider, not a real key.

The external-review fixes at `936bec2` passed both jobs in [run 44199421841](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44199421841). See the [validation record](validation.md) for versioned counts and [PR #5 checks](https://github.com/Smoofers-21/IMS-Bluebox/pull/5/checks) for later commits. PR #5 now includes the application, refreshed docs, and review fixes; it is the single PR to merge. Windows/teacher-server testing, live OMDb, and screenshots remain manual sign-off tasks. Public deployment is after grading with a separate fake-data database.

The [external-review resolution](pr4-review-resolution.md) explains all adopted/deferred findings. The current [SQL catalog](sql-query-catalog.md) follows the runtime source; baseline test/query counts are historical evidence, not a claim about later code.

## Review fixes

- Manager save redirects omit empty optional route values, avoiding a post-save error while preserving a supplied catalog search.
- Overview uses a READ COMMITTED batch; total units are calculated from the materialized warehouse totals, and only five recent movements are fetched. Independent counts/recent activity do not require serializable dashboard locks.
- Login-time password-hash upgrades compare the previously verified hash before writing. A manager's intervening password reset cannot be overwritten by an older login attempt. The update reads `@@ROWCOUNT` explicitly.
- Regression checks cover all three manager create forms, catalog editing, invalid-password messages without echoing the submitted password, concurrent dashboard/stock activity, stale hash upgrades, retirement races, and both sides of the Central-time daylight-saving transition.

- External review also added credential-stamped sessions, immutable edition identity after history, exclusive parent locking before retirement checks, explicit self-edit sign-out, clearer OMDb/correction messages, time-zone fallback, route-only record IDs, and a single PR workflow run per commit. No DDL changes were made.
