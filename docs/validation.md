# Validation record

Updated November 17, 2026. Results below apply to the reviewed Milestone 2 application at commit `6b37024a16ba82a822d6b95ad738b28288401481`. Documentation-only changes do not add application behavior. For any later code changes, check that the latest commit has a successful CI run before merging.

## Verified results

| Check | Result | Evidence and scope |
| --- | --- | --- |
| Local Release build | Passed; zero warnings/errors | Pinned .NET SDK 10.0.404; instructor-template ADO.NET application |
| Local unit/routing suite | 25 passed; 26 SQL Server test groups skipped | SQL groups are intentionally skipped when `BLUEBOX_TEST_SQLSERVER` is unset |
| GitHub unit/routing job | Passed | [Job 134247767347](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44142192232/job/134247767347) |
| GitHub real SQL Server job | Passed | [Job 134247766585](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44142192232/job/134247766585); SQL Server 2022 in an isolated runner-local container |
| Overall Milestone 2 workflow | Success | [Run 44142192232](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44142192232), matching reviewed commit `6b37024` |
| Movie-lookup browser component | Passed | Headless Chromium ran the actual JavaScript with controlled provider responses: safe text rendering, movie selection, metadata fill, fallback, keyboard handling, and stale-request cancellation |
| Runtime SQL syntax | 41 batches parsed as T-SQL | [Runtime query catalog](sql-query-catalog.md); syntax parsing is separate from execution in SQL Server |
| `.slnx` submission conversion | Passed | In a separate extracted source copy: migrated the solution, restored locked packages, built Release with zero warnings/errors, and passed 25 tests with 26 SQL groups skipped; the build/test did not rely on an active `.sln` file |

Milestone 1 also passed [run 44132304808](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44132304808) at commit `02e60c5`. The newer Milestone 2 result covers the expanded implementation. The SQL Server TRX report is available as the `sql-server-test-results` artifact on the run, subject to GitHub sign-in and artifact retention. Exact CI test counts should be read from that report; the local skip count is not a SQL pass count.

## What the SQL Server suite covers

The tests create unique `BlueBoxTest_<guid>` databases, apply the sanitized supplied schema, use fake data, and drop only those generated databases. They exercise real ADO.NET commands and HTTP page workflows, including:

- Authentication, current account status/roles, staff denial of manager operations, and antiforgery.
- Manager item/employee/warehouse forms, catalog editing, duplicate usernames, password validation, retained passwords, and safe hash upgrades after password resets.
- Last-active-manager protection under competing account edits.
- Check-in/out validation, first-balance creation, integer bounds, competing updates, and atomic balance/history rollback.
- Zero-stock retirement and a concurrent retirement versus first-check-in race.
- Linked correction chains, duplicate reversal rejection, insufficient stock, server-derived movement fields, concurrency, and rollback.
- Activity pagination/filter preservation and consistent dashboard totals during stock changes.

OMDb service tests use a fake HTTP provider. The browser component check is not a full browser session against SQL Server or proof of a working live OMDb key.

## Why some workflow runs are canceled

The workflow runs on feature-branch pushes and pull requests. Both events can describe the same commit. Its shared concurrency group and `cancel-in-progress: true` cancel older or duplicate runs when the replacement starts.

A cancellation is not a test failure or a test pass. Check that the completed replacement run for the latest reviewed commit passed both `unit-and-routing-tests` and `sql-server-tests`. Earlier failures are retained in the run history; the reviewed fixes passed the final run linked above.

## Remaining manual verification

| Task | Status | Completion evidence |
| --- | --- | --- |
| Windows/Visual Studio build and browser walkthrough | Pending owner execution | Build the merged solution and walk through staff/manager workflows |
| Teacher's actual SQL Server connection and assigned schema | Pending owner execution | Run from the intended Windows setup with private configuration; do not use this server for automated create/drop tests |
| Live OMDb lookup | Pending private key/setup | Select a movie/title-year match, review metadata, and verify manual fallback |
| Final screenshots and class walkthrough | Pending | Capture fake-data screens after manual verification; follow the [demo guide](demo-guide.md) |
| Public demo hosting | Deferred until after grading | Separate application host and database containing fake data |

The teacher's database has not been accessed or changed from this workspace. Passing disposable SQL Server CI verifies the application against the supplied schema; it does not establish connectivity, credentials, certificate trust, or permissions on the teacher's server.

See [development setup](development.md) to reproduce tests, [Milestone 2 review](milestone-2.md) for manager scenarios, and [class submission](submission.md) for `.slnx` packaging.
