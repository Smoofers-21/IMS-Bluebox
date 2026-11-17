# Milestone 2 review

This branch adds the remaining planned warehouse features using the instructor's `Program.Main` / `MapRoutes` structure and parameterized ADO.NET helpers. It uses the existing five tables and supplied columns. No schema change, teacher-server access, or automatic seeding is needed to start the app.

## Delivered for review

| Feature | Entry point | Key rule |
| --- | --- | --- |
| Overview | `/Overview` | Stock uses bigint sums; all warehouse balances count, including zero-stock warehouses |
| Catalog administration | `/Admin/Items` | Movie/game and format are required; creating an item adds no stock |
| Employees | `/Admin/Employees` | New passwords use the framework hasher; blank edit password preserves the hash |
| Warehouses | `/Admin/Locations` | Name and address required; inactive warehouses cannot move stock |
| Retirement | Item/warehouse status field | Item must have zero stock everywhere; warehouse must have zero stock for every item |
| Corrections | Activity → Correct | Manager supplies an explanation; server derives all movement fields |
| OMDb | Catalog administration → Search movies | Server-side key; review the selected movie's optional metadata before saving |

Managers alone can open or submit the administration/correction pages, including lookup handlers. Transactional services recheck the actor's current manager status before writing. Account changes serialize the directory check/update so competing edits cannot remove the last active manager. Item/location retirement locks the target record and checks stock in the same transaction, coordinating with ordinary stock movements.

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
7. Compare overview totals with inventory and recent activity. Times are labeled Central with CST/CDT; database timestamps remain UTC.

## Validation and remaining work

Local Release build and non-SQL tests run without connecting to the teacher server. Real integration tests use generated disposable SQL Server databases in GitHub Actions. Added coverage includes staff HTTP denial, manager forms/antiforgery, account password preservation and duplicate names, competing manager changes, retirement, correction chains/concurrency/rollback, and dashboard totals. OMDb tests use a fake HTTP provider, not a real key.

Check [PR #4 checks](https://github.com/Smoofers-21/IMS-Bluebox/pull/4/checks) for the latest SQL Server result. Final class readiness also requires a Windows walkthrough against the teacher's database, live OMDb smoke check, and screenshots using fake data. A passing earlier Milestone 1 run is not a substitute for the new run. Public deployment remains after grading, using a separate fake-data database.

Local verification: 25 tests passed; 26 SQL Server test groups were explicitly skipped locally. The movie-lookup JavaScript passed a headless Chromium component check with controlled responses, including safe text rendering, selection, fallback, keyboard handling, and stale-request cancellation. All 42 runtime SQL batches parsed as T-SQL; parsing is not SQL execution.
