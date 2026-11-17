# BlueBox project plan

**Planning baseline:** agreed with the project owner. This document records the original intended behavior; it is not an implementation or test report. Current progress is tracked in the [two-milestone roadmap](roadmap.md).

## Purpose

Build an internal warehouse inventory website for BlueBox, a fictional movie and game rental business. The project is intended for a class demonstration and a resume portfolio. Keep the first version focused on useful stock management rather than unrelated convenience features.

## Scope

### Included

- Browser-based employee sign-in with Staff and Manager roles.
- Movie and game catalog, including separate entries for different formats/platform variants.
- Quantity balances for each item at each warehouse location.
- Check-in, check-out, and permanent movement history.
- Manager corrections linked to the original transaction.
- Item, employee, and location administration.
- OMDb movie lookup for optional posters and age classifications.
- Overview dashboard, searchable inventory, and activity views.
- Fake data for demonstration and validation.

### Excluded

- Customer accounts, rentals, due dates, payments, and late fees.
- Kiosk inventory and transfers tracked to kiosks.
- Streaming, memberships, and promotions.
- Individually numbered physical copies and per-copy condition tracking.
- A game metadata API; game posters and ratings are entered manually.
- Public registration and mandatory first-login password changes.

## Technology decisions

| Component | Decision |
| --- | --- |
| Application language | C# |
| Web framework | ASP.NET Core Razor Pages |
| Database | Microsoft SQL Server |
| Database administration | SQL Server Management Studio (SSMS) |
| Movie lookup | OMDb API, called by the server |
| Data access and authentication libraries | Select during implementation; use supported framework facilities |

Exact .NET/SQL Server versions and hosting are not yet selected. SSMS is the database administration tool; the application connects to SQL Server itself.

## Screen plan

Follow the supplied mockups' navigation and visual direction. Add simple manager administration screens to support the agreed workflows.

| Screen | Purpose | Access |
| --- | --- | --- |
| Sign in | Authenticate an employee | Unauthenticated users |
| Overview | Stock totals, quantities by warehouse, recent movements | Staff and managers |
| Item catalog | Search and filter movies and games | Staff and managers |
| Item details | View catalog information and stock by warehouse | Staff and managers |
| Add/edit item | Maintain items and select an OMDb movie match | Managers |
| Inventory | View quantities by item and warehouse | Staff and managers |
| Check-in/check-out | Preview and record stock movements | Staff and managers |
| Activity log | Review movement details and initiate corrections | Staff and managers; corrections are manager-only |
| Employee management | Create accounts, assign roles, deactivate access | Managers |
| Location management | Maintain and deactivate warehouses | Managers |

Primary navigation: **Sign in → Overview → Catalog / Inventory / Move Stock / Activity**. Managers also have access to administration.

## Agreed workflows

### Add a catalog item

1. A manager enters a name, item type, and media format.
2. For a movie, the manager can search OMDb and select the correct title and release year.
3. The system fills available poster and age-rating information for review. Games use manual entry.
4. The manager saves the item. Creating an item adds no stock; balances begin at zero.
5. Employees add physical stock through check-in.

Posters and age ratings are optional. No match or an unavailable API must not prevent manual item creation. OMDb's `Rated` field is an age classification, not an IMDb review score. No usable rating or poster means the optional field remains empty.

### Check stock in or out

1. Select Check in or Check out, an active item, and an active warehouse.
2. Enter a positive whole-number quantity and optional notes.
3. Review the proposed resulting balance.
4. Confirm the movement.
5. The server validates current stock and saves the balance and movement history together, or saves neither.

The recorded employee comes from the signed-in account. The server supplies the timestamp. Check-out cannot exceed current stock, even if another employee changes the balance after the preview.

### Correct an error

A manager selects the mistaken transaction and records an opposite movement with a required explanation and a reference to the original. The original entry remains unchanged. Corrections also obey stock rules; an insufficient-stock correction cannot force a negative balance. More complex correction cases will be resolved before implementation, without silently rewriting history.

### Retire records and manage accounts

- Archive items or deactivate warehouses only when their stock is zero. An item's stock must be zero across all warehouses.
- Archived items and inactive warehouses cannot receive new movements. Historical references remain available.
- Managers create employee accounts and assign roles. No employee self-registration is provided.
- Inactive or terminated employees cannot access the application.
- The first manager is created during setup. The last active manager cannot be deactivated or demoted.
- Passwords are stored using a supported salted password-hashing facility, never as plaintext. No first-login password-change requirement is planned.

## Planning sources

| Supplied material | Use in this plan |
| --- | --- |
| Bluebox Rentals.docx | Assignment context and Option 2: Internal / Operational interface |
| BlueBox_Warehouse_Conceptual_Model.pdf | Five-entity warehouse model and stock rules |
| BlueBox_Warehouse_Database_Design.pdf | Warehouse workflows and relationships |
| BlueBox_Warehouse_Data_Dictionary.xlsx | Field definitions and composite inventory primary key |
| BlueBox_Screen_Mockups_and_Flow.pdf | Screen layout, navigation, optional poster and rating fields |
| Supplied SQL creation script | Baseline table definitions reviewed during planning |

The original assignment also discusses customer, rental, payment, and reporting functions. The project owner's agreed scope is the warehouse interpretation documented here. Inventory movements do not establish rental revenue, so this plan does not claim sales/rental reporting. Final assignment compliance remains subject to the instructor's rubric.

Where source documents differ, use the agreed decisions here: quantity-based inventory, the item/location composite key instead of a separate inventory ID, and the documented catalog metadata and retirement fields.

## Before implementation

- Confirm compatible .NET and SQL Server versions and obtain connection configuration securely.
- Inspect the actual existing tables before preparing non-destructive schema changes.
- Choose the data access/authentication approach and reconcile it with the employee schema.
- Settle correction safeguards, including repeated corrections, before implementing them.

See the [requirements](requirements.md), [database notes](architecture-and-database.md), and [roadmap](roadmap.md) for the next level of detail.
