# Architecture and database design

**Status:** Milestones 1 and 2 use this structure for inventory, manager tools, linked corrections, and optional movie metadata. The teacher's database connectivity/deployed schema remain unverified. Current implementation and test instructions are in the [development guide](development.md).

## Application structure

Use one ASP.NET Core Razor Pages application. Keep page handling, business rules, and database operations separated enough to test inventory behavior, without introducing microservices or a separate frontend application.

```mermaid
flowchart LR
    U[Employee browser] --> W[ASP.NET Core Razor Pages]
    W --> S[Authorization and inventory services]
    S --> D[(SQL Server)]
    W --> M[Server-side movie lookup]
    M --> O[OMDb API]
```

The browser never needs the SQL Server connection string or OMDb key. The database must remain usable when OMDb is unavailable because movie metadata is optional.

The implementation uses .NET 10, ADO.NET through `Microsoft.Data.SqlClient`, cookie authentication, and ASP.NET Core `PasswordHasher<Employee>`. It maps to the five existing business tables without introducing Identity tables. The SDK is pinned in `global.json`. Deployment remains a later decision.

## Core entities

| Entity | Key | Responsibility |
| --- | --- | --- |
| EMPLOYEE | employee_id | Identity, username, password hash, role, employment status |
| ITEM | item_id | Movie/game catalog entry and physical format |
| LOCATION | location_id | Warehouse name, address, and status |
| CURRENT_INVENTORY | (item_id, location_id) | Quantity on hand and last-updated timestamp |
| TRANSACTION_LOG | transaction_id | Employee-attributed stock movement and optional correction reference |

```mermaid
erDiagram
    EMPLOYEE ||--o{ TRANSACTION_LOG : records
    ITEM ||--o{ TRANSACTION_LOG : has_movements
    LOCATION ||--o{ TRANSACTION_LOG : hosts
    ITEM ||--o{ CURRENT_INVENTORY : has_balances
    LOCATION ||--o{ CURRENT_INVENTORY : holds
    TRANSACTION_LOG o|--o{ TRANSACTION_LOG : referenced_by_correction
```

The current supplied schema includes this self-reference. The foreign key alone permits multiple references; the application serializes corrections against the selected entry and permits one child reversal per entry. Corrections can themselves be corrected, forming a chain. No existing history row is edited or deleted.

Current Inventory resolves the item/location relationship. It has no direct foreign key to Employee or Transaction Log. A movement's item and location identify the balance it affects.

## Baseline SQL review

The supplied creation script already defines:

- The five core tables with generated identifiers where applicable.
- A composite primary key on Current Inventory.
- Unique employee usernames.
- Positive movement quantities and nonnegative inventory balances.
- Movie/game and check-in/check-out value constraints.
- Foreign keys with `ON DELETE NO ACTION`.
- UTC timestamp defaults using `SYSUTCDATETIME()`.

The script requires a real expected database name and refuses to create tables when any of the five target names already exists. It is a fresh-schema installer, not an incremental update script. Inspect existing tables and prepare targeted changes later; do not drop/recreate populated tables to apply these plans.

## Schema alignment

The latest supplied DDL already includes the columns and constraints below. Milestone 2 uses them without requiring another migration.

The [one-time SQL migration](../database/README.md) exists for older baseline installations. Do not rerun it on the already-aligned teacher database.

| Table | Aligned field / rule | Reason |
| --- | --- | --- |
| ITEM | Nullable `poster_url` | OMDb movie poster or manually entered game poster |
| ITEM | Nullable `age_rating` | Age classification, such as PG-13 or T; not a review score |
| ITEM | Required `item_status` constrained to ACTIVE/ARCHIVED | Retire catalog records without removing history |
| TRANSACTION_LOG | Nullable `corrects_transaction_id`, referencing TRANSACTION_LOG | Link manager corrections to their original movement |
| EMPLOYEE | Constrain `access_role` to STAFF/MANAGER | Prevent invalid roles |
| EMPLOYEE | Constrain employment status to ACTIVE/INACTIVE/TERMINATED | Match the agreed access rules |
| LOCATION | Constrain status to ACTIVE/INACTIVE | Match warehouse availability rules |
| ITEM | Reject blank `media_format` | Enforce the required catalog field |

Runtime validation follows the supplied field lengths. Store movie metadata on the selected catalog item; do not require an external API call every time the catalog is opened. Consider a provider identifier if needed to retain the selected OMDb match, but it is not an agreed required field yet.

## Inventory consistency

Every movement must perform these steps as one database transaction:

1. Check the employee's current access and the item/location's active status.
2. Validate direction and positive whole-number quantity.
3. Safely find or create the item/location balance and apply the movement against current stock.
4. Insert the matching movement entry with server-derived employee/time and correction details if applicable.
5. Refresh `last_updated_at` and commit, or roll back all changes.

Use serializable SQL Server transactions, `UPDLOCK`/`HOLDLOCK` on the inventory key, and a conditional balance update. An earlier browser preview is not sufficient protection against competing check-outs. Handle simultaneous first check-ins to the same item/location without duplicate balances.

The nonnegative quantity constraint is a useful final guard, but does not by itself ensure balance/history consistency. Timestamp defaults initialize rows; they do not automatically update `last_updated_at` after later movements.

## History and retirement

- No application workflow directly edits stock balances or edits/deletes log entries.
- Foreign keys prevent deleting referenced parents; they do not prevent direct deletion of the log itself. Application/database access must respect history retention.
- Corrections retain both entries and reference the same item/location with the opposite direction and a required explanation.
- Archive an item only when all its warehouse balances are zero. Deactivate a warehouse only when all its balances are zero.
- Manager authorization and zero-stock checks must be enforced when saving, not just when showing a button.

## Configuration

Configure SQL Server connectivity and the OMDb key securely outside source control using the configuration mechanism selected for the application. No credentials, connection string, or API key is included in these documents. Database access and OMDb access remain unverified until implementation setup.
