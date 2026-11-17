# Instructor template adaptation and code review

Milestone 1 now uses the supplied `WebAppWithDB_Starter_v2` foundation. The original paste contained live connection details; it is not committed. Those values are configured privately.

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

Razor Pages continue to render the existing screens, encode output, and handle form antiforgery. They call the ADO.NET helpers through small repositories/services. The template's demonstration datetime HTML and Bootstrap CDN are replaced by the actual local BlueBox screens/assets. If the instructor additionally forbids Razor Pages or requires all HTML inside Program, that is a separate requirement to confirm; the supplied starter does not explicitly state that restriction.

The template's hardcoded DB fields map to `Database:Server`, `Database:Name`, `Database:Username`, and `Database:Password`, or an existing private `ConnectionStrings:BlueBox`. The configurable `Database:RoutePrefix` preserves the assigned-path concept without publishing an account name in source. See [development setup](development.md).

## Corrections and optimizations

- Removed Entity Framework, its model context, SQLite, and unused entity types.
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

## Validation limits

The earlier 27-check SQLite result applies to the former EF implementation, not this refactor. Current local tests cover configuration, basic validation, anonymous routing, antiforgery, role policy, and headers. SQL Server integration scenarios are explicitly skipped locally without a disposable instance.

The SQL Server CI job applies the supplied current schema to generated test databases and exercises the real ADO.NET stock/HTTP workflows, including rollback and competing updates. Live verification was left to Windows/GitHub at the project owner's request. No teacher database schema, records, or stock were changed from this workspace.
