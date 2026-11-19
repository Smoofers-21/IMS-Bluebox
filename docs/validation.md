# Validation record

Updated November 19, 2026. PR #5 was merged into main at `13b4f43`. The [main-review follow-up](main-review-resolution.md) makes no DDL changes. Every result below is tied to the revision/run actually checked; use the current head’s completed [GitHub Actions run](https://github.com/Smoofers-21/IMS-Bluebox/actions/workflows/build.yml) for a later revision.

## Azure portfolio preparation

The owner reports creating the Azure SQL portfolio database, running the fresh-schema query, and creating an App Service website. The [public address](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net) is documented with launch pending. No Azure credentials were supplied to this workspace and no live connection, DDL, seed operation, Azure resource creation, or website publishing was performed here. The [setup guide](azure-portfolio-setup.md) keeps cloud verification explicit.

The fresh installer wraps the existing reference DDL in target/existing-object guards and an atomic transaction, with no `USE` or `GO` requirement. Existing reference definitions and migration scripts are unchanged. Disposable SQL regressions cover fresh installation plus stock use, rerun rejection with retained data, and wrong-target rejection.

PR #7 commit `ccaa453`, merged into `main` at `1959a24`, passed [run 44627140027](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44627140027). Both jobs succeeded; the SQL-enabled suite passed **91 tests, 0 failures** (unit tests plus expanded SQL cases), including the new installer regressions. This result was checked against the run's commit, success status, and test-count annotation. It does not verify live Azure connectivity or free-tier settings.

Preparation checks at that revision: locked restore and Release tests passed locally (**35 passed, 37 SQL groups skipped, 0 failures**). Both the reusable script and owner-targeted download parse as T-SQL. All 61 source DDL batches match the supplied reference; documentation links and the regenerated Word plan were checked. The Azure smoke test remains pending.

The November 19 launch-documentation update changes only documentation. Locked restore and Release tests again passed locally (**35 passed, 37 SQL groups skipped, 0 failures**); all **142 local documentation links/fragments** passed checks, and the regenerated Word plan matched its Markdown paragraphs and table cells. Public hostname links were checked for consistent spelling, not live application availability. No application, DDL, or deployment workflow changed.

## Main-review follow-up verification

PR #6 review fixes at source commit `1f02ef0` passed [run 44419416536](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44419416536): both jobs succeeded and the SQL-enabled suite passed **89 tests, 0 failures** (unit tests plus expanded SQL cases). The CU28 container started successfully in that run. Local locked restore and Release tests passed **35 tests**, with **35 SQL Server groups intentionally skipped**. Later documentation-only commits have their own runs; use [PR #6's latest checks](https://github.com/Smoofers-21/IMS-Bluebox/pull/6/checks) for the current head.

The expanded regression follows actual rendered pager URLs, parses and posts to new/edit form actions, verifies edits update the original row without duplicates, and checks New item exits editing. Local tag-helper tests cover absent and existing item IDs, preventing invalid empty numeric route values. The rate-limit response content type is also checked. No DDL upgrade is needed against the last supplied schema.

### Earlier follow-up evidence

- Source commit `9248e83`: locked restore and Release compilation/test succeeded locally: **32 passed, 35 SQL Server groups skipped, 0 failures**. No disposable SQL Server is configured here. These local results do not verify the new SQL queries/workflows.
- New local coverage: POST-only login rate limiting and retry response, untrusted forwarded-address header behavior, versioned/unversioned/mismatched asset caching, security headers, and generic 500/503 error classification.
- Disposable SQL regression coverage includes unknown-user hash verification, secure login cookies, catalog/inventory/admin paging, all item/location pairs, filters, invalid manager posts/save redirects, and exact final-page boundaries. These cases passed in the replacement run cited above.
- Correction: the original PR #6 image digest could not be resolved by Docker. [Run 44416004511](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44416004511) failed before SQL tests ran. The earlier HTTP-only registry check was insufficient proof of a pullable image. The workflow now uses the specific CU28 tag; actual startup and SQL test execution succeeded in the replacement run above.
- Database scripts and sanitized reference are unchanged from `13b4f43`; no teacher connection or DDL operation was performed.

The versioned results below are historical baseline evidence and are not proof of this follow-up.

## Verified results

The schema-compatible fixes at commit `936bec2` passed [run 44199421841](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44199421841):

| Check | Result | Scope |
| --- | --- | --- |
| Local Release compile/test | Passed with warnings treated as errors | SDK 10.0.404, locked restore, instructor-template ADO.NET application |
| Local unit/routing/provider tests | 29 passed; 33 SQL Server groups intentionally skipped | No disposable SQL Server was configured locally |
| GitHub unit/routing job | Passed | Same application and regression sources |
| GitHub SQL-enabled suite | 81 passed, 0 failed, 81 total | Includes unit tests and all expanded real SQL Server cases, not 81 SQL-only tests |
| Schema | Supplied five-table reference | Applied only in generated disposable SQL Server 2022 databases; no teacher-server changes |
| New regression coverage | Passed in the SQL-enabled suite | Session revocation/rehash, edition identity, self-edit messages, route binding, retired corrections, and observed parent-lock ordering |

The SQL job publishes a TRX artifact (`sql-server-test-results`) and a test-count notice. Counts above are evidence for `936bec2`, not permanent expected totals; use the newest run/report for later revisions. Newer documentation-only commits can have their own CI runs.

## Historical baseline evidence

- Milestone 1 commit `02e60c5` passed [run 44132304808](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44132304808).
- The pre-review Milestone 2 baseline `6b37024` passed [run 44142192232](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44142192232), with 25 local tests passed and 26 SQL groups skipped locally. Its SQL-enabled job passed separately in GitHub.
- The earlier movie-lookup browser component check ran the actual JavaScript in headless Chromium with controlled responses: safe text rendering, selection, metadata fill, fallback, keyboard handling, and cancellation. This is not a live OMDb or full browser-to-SQL check.
- The `.slnx` submission layout was verified in an extracted baseline source copy: migration, locked restore, Release build with zero warnings/errors, and 25 tests passed with 26 SQL groups skipped, without relying on an active `.sln` file. The solution/project paths are unchanged by these review fixes.
- The [SQL query catalog](sql-query-catalog.md) is regenerated from current runtime literals. T-SQL syntax parsing and real SQL Server test execution are distinct checks.

## What the SQL Server suite covers

The tests create unique `BlueBoxTest_<guid>` databases, apply the sanitized supplied schema, use fake data, and drop only those generated databases. They exercise real ADO.NET commands and HTTP page workflows, including:

- Authentication, current account status/roles, staff denial of manager operations, and antiforgery.
- Manager item/employee/warehouse forms, catalog editing, duplicate usernames, password validation, retained passwords, and safe hash upgrades after password resets.
- Last-active-manager protection under competing account edits.
- Check-in/out validation, first-balance creation, integer bounds, competing updates, and atomic balance/history rollback.
- Zero-stock retirement and a concurrent retirement versus first-check-in race.
- Linked correction chains, duplicate reversal rejection, insufficient stock, server-derived movement fields, concurrency, and rollback.
- Activity pagination/filter preservation and warehouse-derived dashboard totals during stock changes.
- Follow-up catalog/inventory/admin pagination, unknown-user hash verification, and secure session cookies (new cases; see current-head CI results above).
- Password/username session revocation, valid login after a hash upgrade, edition-identity protection, self-account sign-out, route-ID tampering, and deterministic retirement lock ordering without deadlock retries.

OMDb service tests use a fake HTTP provider. The browser component check is not a full browser session against SQL Server or proof of a working live OMDb key.

## Workflow run selection

CI runs on pull requests and main pushes. The concurrency key uses the PR number/ref, so only superseded runs in that PR/ref are canceled. Feature-branch pushes do not launch a second duplicate workflow alongside the PR event. Older canceled push/PR duplicates remain in historical run lists; a cancellation is neither a pass nor a failure. Use the successful completed run for the current head.

## Remaining manual verification

| Task | Status | Completion evidence |
| --- | --- | --- |
| Windows/Visual Studio build and browser walkthrough | Pending owner execution | Build the merged solution and walk through staff/manager workflows |
| Teacher's actual SQL Server connection and assigned schema | Historical class check; outside current portfolio release | Run from the intended Windows setup with private configuration; do not use this server for automated create/drop tests |
| Live OMDb lookup | Pending private key/setup | Select a movie/title-year match, review metadata, and verify manual fallback |
| Final screenshots and class walkthrough | Pending | Capture fake-data screens after manual verification; follow the [demo guide](demo-guide.md) |
| Azure portfolio schema/connection, free-tier configuration, and public hosting | Owner reports resources created and schema query run; application launch and live verification pending | Confirm the five tables, private settings, SQL firewall, F1/free SQL limits, publishing, demo accounts, browser workflows, and idle resume |

The teacher's database has not been accessed or changed from this workspace. Passing disposable SQL Server CI verifies the application against the supplied schema; it does not establish connectivity, credentials, certificate trust, or permissions on the teacher's server.

See [development setup](development.md) to reproduce tests, [Milestone 2 review](milestone-2.md) for manager scenarios, and [class submission](submission.md) for `.slnx` packaging.
