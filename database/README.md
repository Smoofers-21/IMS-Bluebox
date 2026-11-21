# BlueBox SQL Server scripts

These scripts target the five-table SQL Server design supplied during planning. The C# application uses this schema through ADO.NET and does not apply DDL automatically. No script has been run against the teacher's server by this project workspace.

The latest supplied DDL already contains the planned columns and constraints, with `poster_url NVARCHAR(2048)`. **Do not rerun 001 against that updated database.** A sanitized [current schema reference](reference/current-schema.sql) is retained for empty test databases; it is not an upgrade script.

For the new web-only portfolio database, use the [Azure setup guide](../docs/azure-portfolio-setup.md). It uses Azure Query editor and the guarded fresh installer; SSMS is not required. The older scripts and teacher-reference instructions below remain for the original class setup.

## Azure portfolio process

The owner reports creating the Azure SQL database and running the fresh installer. Apply the new [index-only upgrade](azure/002_add_portfolio_indexes.sql) to that existing portfolio schema after setting its expected database name. It adds four nonunique indexes without changing records or business definitions. Do not rerun creation scripts to publish the website or populate accounts. Live schema and application connectivity still need verification.

1. For a new installation only, create an empty Azure SQL database using the free offer with monthly-limit auto-pause, then run the guarded Azure installer through **Query editor**.
2. Store the database connection privately in App Service as `ConnectionStrings__BlueBox`; use the actual password only in Azure settings. Keep encryption and certificate validation enabled.
3. Allow the App Service's documented outbound IP addresses in the SQL server firewall. Visitors connect to the website, not directly to SQL Server.
4. [Publish through GitHub Actions](../docs/github-actions-deployment.md) and run the separately supplied private account/location starter SQL once if needed. The portfolio starts with one manager, four staff, and Fayetteville/Bentonville warehouses; the owner will add items and stock after launch. Do not use the older `--seed-demo` command for this setup. Neither publishing nor ordinary startup changes the schema or seeds accounts.
5. Verify login, stock updates, history, and idle database resume through the [portfolio website](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net), which is awaiting launch.

See the [full Azure instructions](../docs/azure-portfolio-setup.md) for private configuration, publishing, and account setup. Automated create/drop tests run only against disposable SQL Server instances, never the portfolio database.

## Which file to use

| Situation | Script |
| --- | --- |
| New, empty Azure SQL portfolio database | Use [azure/001_create_portfolio_schema.sql](azure/001_create_portfolio_schema.sql); set its expected database name first |
| Existing portfolio tables created before the performance indexes | Use [azure/002_add_portfolio_indexes.sql](azure/002_add_portfolio_indexes.sql); set its expected database name first; preserve tables and data |
| The tables match the latest supplied DDL, including statuses, metadata, and correction link | No migration needed; configure the application connection |
| The original five-table baseline exists without the alignment columns/constraints | Review and run [001_align_warehouse_schema.sql](001_align_warehouse_schema.sql) once |
| The schema is partial or its version is unknown | Compare with the reference and review differences before choosing a migration |
| You are preparing a new, empty BlueBox development database | Run [000_create_baseline.sql](000_create_baseline.sql), then 001 |

`000` preserves the original creation script supplied by the project owner. It is not an upgrade script and refuses to run if a target table already exists. Neither script creates a database or grants permissions.

The Azure installer now includes `IX_TRANSACTION_LOG_corrects_transaction_id` (filtered to non-NULL references), `IX_TRANSACTION_LOG_item_id`, `IX_TRANSACTION_LOG_location_id`, and `IX_CURRENT_INVENTORY_location` (including `quantity_on_hand`). The matching `002` upgrade skips existing same-named indexes and is rerunnable; inspect manually altered definitions before applying it. The original class migration and sanitized reference remain unchanged. Indexes support lookups but do not replace transaction locks or manager authorization.

## Apply the one-time upgrade to an older baseline in SSMS

1. Use a copy of your BlueBox database to check compatibility first when available. Confirm that you are changing only your assigned database.
2. Open `001_align_warehouse_schema.sql` in a fresh SSMS query window connected to the intended server.
3. Select your BlueBox database in the database dropdown.
4. Replace `YourBlueBoxDatabaseName` in `@ExpectedDatabase` with that database's exact name. Do not add credentials to the script.
5. Execute the entire file together. It requires permission to alter the tables; it does not need server-administrator access.
6. Confirm the success message. The first result set should show four new columns. The second should show six check constraints and one foreign key, all with `is_disabled = 0` and `is_not_trusted = 0`.
7. Refresh the tables in Object Explorer to see the added columns.

The migration is intentionally one-time. Rerunning it stops with a message rather than silently skipping an unknown or partial schema. Do not rerun `000` or drop existing tables to recover from an error.

## What changes

| Table | Change |
| --- | --- |
| ITEM | Nullable `poster_url NVARCHAR(2048)` |
| ITEM | Nullable `age_rating VARCHAR(20)` |
| ITEM | `item_status VARCHAR(10) NOT NULL`, default ACTIVE, allowed ACTIVE/ARCHIVED |
| ITEM | Reject blank/ASCII-whitespace-only media formats |
| EMPLOYEE | Restrict roles to STAFF/MANAGER and employment statuses to ACTIVE/INACTIVE/TERMINATED |
| LOCATION | Restrict statuses to ACTIVE/INACTIVE |
| TRANSACTION_LOG | Nullable `corrects_transaction_id BIGINT` referencing an original transaction, with an index |
| TRANSACTION_LOG | Corrections must have nonblank notes and cannot reference themselves |

Allowed role/status values use exact uppercase spelling without trailing spaces, consistently with the supplied baseline's item/movement types. Optional metadata stays NULL on existing rows. All existing items become ACTIVE. No quantities or historical movements are rewritten.

## If the update stops

- **Wrong database:** select the correct database and set `@ExpectedDatabase` to its exact name.
- **Missing tables:** compare your schema with the supplied baseline; do not create duplicates in another schema.
- **Column already exists:** inspect the current definitions. The update may already have been applied, or a manual change needs reconciliation.
- **CHECK constraint conflict:** inspect the named table and constraint. Existing invalid roles/statuses or blank media formats must be corrected intentionally before retrying. The migration does not guess replacements or disable validation.
- **Permission error:** the database owner may need to grant the appropriate rights or apply the reviewed script.

Changes within the migration transaction roll back if a schema or data constraint fails. This does not replace checking that the script targets the expected schema and database.

## What DDL does not implement

These application-level rules are implemented by the C# services and authorization policies; DDL alone does not provide them:

- Manager-only corrections, with the same item/location and opposite movement direction as the original.
- Active employee, item, and warehouse checks.
- Zero stock before archiving an item or deactivating a warehouse.
- A single atomic balance update and history insert, safe under competing movements.
- Updating `last_updated_at` on each stock change; its default only initializes a new row.
- Permanent history through application behavior and appropriate database access.
- Last-active-manager protection.
- Fixed item type/format after history exists, and credential-stamped session revocation after username/password changes, using the existing columns.

The correction foreign key identifies an original transaction; it does not, by itself, enforce manager authorization or compare values between the two rows. The application permits one full reversal per history entry, including correction entries, forming a chain. It locks the selected entry and checks for an existing child within the same transaction. No uniqueness constraint alone enforces that one-child policy for direct SSMS writes. See the [correction guide](../docs/milestone-2.md#agreed-correction-policy).

## Verified schema use

The SQL Server CI suite applies the reference, guarded Azure installer, and index-only upgrade only in generated disposable databases. PR #8 source commit `0458754` passed [run 44633770879](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44633770879), with 105 tests passed across the SQL-enabled suite, including unit tests and expanded SQL cases. This is SQL Server 2022 verification, not a live Azure connectivity check; see the [validation record](../docs/validation.md). The suite never targets the teacher's or portfolio server. All three schema resources are embedded by the test project, so retain the `database` folder when [packaging the solution](../docs/submission.md).
