# BlueBox SQL Server scripts

These scripts target the five-table SQL Server design supplied during planning. They are separate from the future C# application. No script has been run against the teacher's server by this project workspace.

The latest supplied DDL already contains the planned columns and constraints, with `poster_url NVARCHAR(2048)`. **Do not rerun 001 against that updated database.** A sanitized [current schema reference](reference/current-schema.sql) is retained for empty test databases; it is not an upgrade script.

## Which file to use

| Situation | Script |
| --- | --- |
| The five warehouse tables already exist | Run [001_align_warehouse_schema.sql](001_align_warehouse_schema.sql) only |
| You are preparing a new, empty BlueBox development database | Run [000_create_baseline.sql](000_create_baseline.sql), then 001 |

`000` preserves the original creation script supplied by the project owner. It is not an upgrade script and refuses to run if a target table already exists. Neither script creates a database or grants permissions.

## Apply the update in SSMS

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

The future C# stock-operation service must still enforce:

- Manager-only corrections, with the same item/location and opposite movement direction as the original.
- Active employee, item, and warehouse checks.
- Zero stock before archiving an item or deactivating a warehouse.
- A single atomic balance update and history insert, safe under competing movements.
- Updating `last_updated_at` on each stock change; its default only initializes a new row.
- Permanent history through application behavior and appropriate database access.
- Last-active-manager protection.

The correction foreign key identifies an original transaction; it does not, by itself, enforce manager authorization or compare values between the two rows. There is no uniqueness constraint deciding repeated-correction policy. That policy remains to be settled before building the correction workflow.
