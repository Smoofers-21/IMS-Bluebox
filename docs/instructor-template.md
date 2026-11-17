# Instructor template adaptation and code review

Both milestones use the supplied `WebAppWithDB_Starter_v2` foundation. The original paste contained live connection details; it is not committed. Those values are configured privately.

## Preserved foundation

| Starter element | BlueBox implementation |
| --- | --- |
| Explicit Program class and Main | `src/BlueBox.Web/Program.cs`, with async Main for setup commands |
| MapRoutes and HandleRoot | Root redirect, configurable assigned landing path, workspace redirect |
| HandleError | Generic HTML failure response; technical details are logged server-side |
| Response security headers | nosniff, frame restrictions, CSP, robots header |
| GetConnectionString | `Data/SqlDatabase.cs`, using private configuration and SqlConnectionStringBuilder |
| FillDataTableViaSqlAsync / FillDataTableViaCommandAsync | Same helper approach with SqlCommand and DataTable |
| ExecSqlStringAsync / ExecSqlCommandAsync | Same helper approach with async SqlConnection/ExecuteNonQuery |
| Microsoft.Data.SqlClient and System.Data | Direct ADO.NET; no Entity Framework dependency |

Razor Pages continue to render the existing screens, encode output, and handle form antiforgery. They call the ADO.NET helpers through small repositories/services. The template's demonstration datetime HTML and Bootstrap CDN are replaced by the actual local BlueBox screens/assets. The explicit Program/Main/MapRoutes foundation and direct ADO.NET data-access approach remain in place.

The template's hardcoded DB fields map to `Database:Server`, `Database:Name`, `Database:Username`, and `Database:Password`, or an existing private `ConnectionStrings:BlueBox`. The configurable `Database:RoutePrefix` preserves the assigned-path concept without publishing an account name in source. See [development setup](development.md).

## Corrections and optimizations

- Use direct ADO.NET without Entity Framework, SQLite, or extra Identity tables.
- Repaired the pasted Markdown-style CDN links and malformed quoted markup by using local assets and valid C#.
- Use bounded, parameterized SELECTs and explicit SQL parameter types/lengths. Search text is never interpolated into SQL.
- Use `COUNT_BIG`/bigint stock totals where aggregation can exceed a single warehouse's int balance.
- Dispose connections, commands, readers, and transactions deterministically. Connection pooling remains available in normal application use.
- Removed `Task.Run` around DataTable loading. Connection opening and query execution stay async; small/bounded result loading stays synchronous.
- Match retryable SQL errors by error number, rather than localized message text. Retry at most twice after the first attempt.
- Do not retry ordinary writes on timeouts or disconnects with unknown commit outcomes. Retry stock movements only as a complete transaction after a confirmed deadlock rollback.
- Use serializable transactions and indexed `UPDLOCK`/`HOLDLOCK` balance reads, including absent balances. Apply the balance and history insert together.
- Use server UTC timestamps and `SELECT @@ROWCOUNT` for reliable balance-write verification.
- Fetch one selected balance for the live preview instead of serializing every warehouse balance into the page. Cancel outdated preview requests.
- Retain server validation regardless of preview values; browser-supplied employee IDs are ignored.
- Keep password hashing, active-session validation, role policy, and local-return-URL checks.

## Supplied DDL review

The latest DDL contains the five tables, planned statuses/metadata, correction link, nonnegative stock/positive-movement constraints, and historical foreign keys. `poster_url` is **NVARCHAR(2048)**; the fresh-install migration/reference now agree with that type. Do not rerun the one-time upgrade against the already-updated database.

A sanitized [schema reference](../database/reference/current-schema.sql) preserves the supplied definitions without the real database name. It is for an empty test database, not a migration. A [runtime query catalog](sql-query-catalog.md) documents application SELECT/INSERT/UPDATE statements separately from DDL and test utilities. No application DELETE workflow exists.

## Verified validation and limits

The external-review fixes at `936bec2` passed [run 44199421841](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44199421841), including the SQL-enabled suite. Versioned local/CI counts are recorded in the validation record; current results are on PR #5 checks. The SQL job applies the supplied current schema to generated disposable databases and exercises real stock/HTTP workflows, rollback, and competing updates. See the [validation record](validation.md).

Windows access to the teacher's actual server and a live OMDb smoke check remain pending. No teacher schema, records, or stock were changed from this workspace.

## PR #2 review fixes

- Movement inputs have no POST-time defaults. Missing fields fail required validation; initial quantity/direction defaults are assigned on GET only.
- Setup accepts 12–256 character passwords, sharing the login maximum through `PasswordPolicy`.
- Activity uses bounded 100-row pages with a one-row lookahead, deterministic ordering, and filter-preserving Older/Newer links. No full-history download or separate total-count query is needed.
- Local regression tests exercise actual model binding and password boundaries. SQL Server regressions verify unchanged stock/history for incomplete posts, maximum-length-password sign-in, and navigation across more than 100 filtered movements. These scenarios passed in disposable SQL Server CI; the teacher-server Windows walkthrough remains separate.

## Milestone 2 review fixes

- Omit empty optional route values when redirecting after manager saves; HTTP tests cover all three administration forms and catalog editing.
- Read overview in one READ COMMITTED batch, deriving total units from returned warehouse totals. Stock/history writes continue using serializable transactions.
- Compare the previously verified password hash before a login-time upgrade, so a concurrent manager password reset cannot be overwritten. Read `@@ROWCOUNT` explicitly.
- Exercise manager/retirement/correction concurrency, atomic rollback, safe metadata handling, and Central-time daylight-saving transitions. See the [Milestone 2 review guide](milestone-2.md).

For the consolidated PR #5 external-review fixes, see [the resolution](pr4-review-resolution.md). Parent X locks coordinate retirement, credential stamps revoke stale sessions, and format/type cannot change after history exists. These changes use the supplied schema.
