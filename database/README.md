# BlueBox Azure SQL database

BlueBox uses **Azure SQL Database** for its portfolio website. The application accesses the five-table SQL Server-compatible schema through parameterized ADO.NET. Database administration uses Azure Portal's **Query editor**; no desktop database tool is required.

**Website deployment: pending.** The owner reports completing the schema/index scripts, app settings, and SQL firewall rules. Authenticated application access and the live stock workflows remain to be verified after deployment. See the [project README](../README.md), [Azure setup](../docs/azure-portfolio-setup.md), and [manual deployment guide](../docs/github-actions-deployment.md).

## Current schema

| Table | Purpose |
| --- | --- |
| `EMPLOYEE` | App accounts, password hashes, Staff/Manager roles, and employment status |
| `ITEM` | Movie/game catalog editions, optional poster/age classification, and archival status |
| `LOCATION` | Warehouses, addresses, and active/inactive status |
| `CURRENT_INVENTORY` | Quantity and last-update time for each item/location pair |
| `TRANSACTION_LOG` | Attributed stock movements and links to correction entries |

The inventory primary key is `(item_id, location_id)`. Foreign keys preserve relationships, and constraints reject invalid statuses, nonpositive movement quantities, and negative balances. Passwords use the application's supported salted hashing facility. `ITEM.poster_url` supports nullable `NVARCHAR(2048)` metadata.

## Which script to use

| Situation | File/action |
| --- | --- |
| New, empty Azure SQL database | Run [azure/001_create_portfolio_schema.sql](azure/001_create_portfolio_schema.sql) after setting its expected database name |
| Existing aligned tables created before the portfolio indexes | Run [azure/002_add_portfolio_indexes.sql](azure/002_add_portfolio_indexes.sql) after setting its expected database name |
| Current tables and all four indexes already installed | No schema script is needed for publishing or README updates |
| Reference definitions for inspection or disposable tests | [reference/current-schema.sql](reference/current-schema.sql); this is not an upgrade script |
| Partial or manually changed schema | Compare definitions and inspect differences before choosing an upgrade |

The older [000_create_baseline.sql](000_create_baseline.sql) and [001_align_warehouse_schema.sql](001_align_warehouse_schema.sql) remain as historical schema revisions. They are not the current Azure installation path; do not run them against an already aligned database.

For a fresh installation, select the intended database in Query editor, set `@ExpectedDatabase`, and execute the whole Azure installer. It creates the schema and indexes together in a guarded transaction, without creating a database or inserting records. It refuses existing BlueBox objects. Do not drop tables or rerun installation to fix a connection or publishing problem.

The index-only upgrade preserves existing records, adds missing portfolio indexes in one transaction, and can be rerun. Inspect manually changed index definitions first because it does not replace existing same-named indexes.

## Portfolio indexes

- `IX_TRANSACTION_LOG_corrects_transaction_id`: filtered lookup for non-NULL correction references.
- `IX_TRANSACTION_LOG_item_id`: movement history by item.
- `IX_TRANSACTION_LOG_location_id`: movement history by warehouse.
- `IX_CURRENT_INVENTORY_location`: warehouse stock lookup, including `quantity_on_hand`.

These are nonunique performance indexes. They support lookups without changing the business columns or replacing application authorization and transaction rules. The sanitized reference retains the table/constraint definitions; the Azure installer adds these four secondary indexes.

## Connection and initial data

1. Store the complete encrypted connection string privately in App Service as `ConnectionStrings__BlueBox`, or use the supported separate `Database__*` fields. Keep certificate validation enabled.
2. Allow each App Service outbound IP individually in the SQL server firewall. Browser visitors connect to the website; they do not connect directly to SQL.
3. If needed, run the separately supplied **private account/location starter SQL** once in the intended database. It requires all five tables to be empty and inserts employees/locations atomically. Keep its login hashes and separate initial-password file outside GitHub.
4. The intended initial dataset is **one active manager, four active staff, and Fayetteville, AR/Bentonville, AR warehouses**. It deliberately contains no items, current inventory, or transactions. The owner adds catalog entries and stock through the application after launch.
5. Publish through the manual Actions workflow, then sign in and verify the Azure connection and stock workflows. A successful anonymous sign-in-page check alone does not query SQL.

Do not run `--seed-demo` for this portfolio setup: that optional full sample-data command also creates catalog items, balances, and opening history. Publishing and ordinary startup apply no SQL scripts and seed no data. README-only changes require no database update or website redeployment.

## Rules implemented by the application

The services enforce manager-only corrections, active-record checks, zero stock before item/warehouse retirement, last-active-manager protection, session revocation after credential changes, and fixed edition type/format once history exists. Each stock operation saves the quantity and audit entry together in a serializable transaction.

Corrections add an opposite movement with an explanation; they preserve the original entry. Each movement may have one correction, and a correction can itself be corrected once to form a chain. That policy is enforced by the application transaction; the correction foreign key and nonunique index do not enforce all business rules for direct SQL writes. See the [correction guide](../docs/milestone-2.md#agreed-correction-policy).

## Validation

The merged application baseline `cb72cd6` passed [main CI run 44866843871](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44866843871), with both jobs successful and **105 tests passed, 0 failures** in the SQL-enabled suite (unit tests plus expanded SQL cases). CI applies the reference, guarded Azure installer, and index-only upgrade only in generated disposable SQL Server 2022 databases. It tests stock rules, rollback, concurrent updates, correction chains, installer guards, index definitions, and data preservation.

Never configure automated create/drop tests with the Azure portfolio server. Disposable SQL Server verification and live Azure verification are separate: the latter still requires actual sign-in, workflows, and idle-resume checks. The test project embeds all three schema resources, so retain the `database` folder when building/testing the solution.
