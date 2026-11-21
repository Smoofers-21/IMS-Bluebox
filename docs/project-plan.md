# BlueBox project plan

**Project baseline, updated November 20, 2026:** this document records the agreed scope and current design decisions. Both milestones’ application features are implemented. See the [roadmap](roadmap.md) for remaining sign-off tasks and the [validation record](validation.md) for test evidence.

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
- Public staff demo credentials in an expandable login-page panel and README, with copy buttons and normal sign-in; shared stock changes are disclosed and manager access stays restricted.

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
| Application language | C# on .NET 10; SDK 10.0.404 pinned in `global.json` |
| Web framework | ASP.NET Core Razor Pages |
| Database | Microsoft SQL Server; CI uses a disposable SQL Server 2022 instance |
| Database administration | Azure browser tools for the portfolio; SSMS remains an optional tool for the original class database |
| Movie lookup | OMDb API, called by the server |
| Data access | ADO.NET with Microsoft.Data.SqlClient 7.1.2 and the instructor’s SqlConnection/SqlCommand/DataTable helpers |
| Authentication | Cookie authentication and ASP.NET Core `PasswordHasher<Employee>`; Staff/Manager authorization |
| Solution and submission | `BlueBox.sln` in source/CI; generate `.slnx` for submission when requested |
| Portfolio deployment | Manual GitHub Actions deployment from main after its build/SQL checks pass; SDK from `global.json`, existing App Service F1 host |

The portfolio uses Azure SQL browser tools for administration and Azure App Service F1 for website hosting, with separate fake data. SSMS remains optional for the original class setup. The owner reports creating the Azure resources, applying schema/index SQL, and configuring app settings/firewall access. The agreed starter data is one manager, four staff, and Fayetteville/Bentonville warehouses; items, current inventory, and transactions remain empty until the owner adds them through the application. Initial setup files stay private. Publishing, live connectivity, starter-data execution, free-tier confirmation, and browser verification remain pending. See [Azure setup](azure-portfolio-setup.md) and [GitHub Actions deployment](github-actions-deployment.md).

Website address, awaiting launch: [BlueBox on Azure](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net). Visitors will use a browser; no Visual Studio or database administration tool is required to use the deployed demo.

## Screen plan

The implemented screens follow the supplied mockups’ navigation and visual direction, with manager administration pages for the agreed workflows.

| Screen | Purpose | Access |
| --- | --- | --- |
| Sign in | Authenticate an employee | Unauthenticated users |
| Overview | Stock totals, quantities by warehouse, recent movements | Staff and managers |
| Item catalog | Search, filter, and page through movies and games | Staff and managers |
| Item details | View catalog information and stock by warehouse | Staff and managers |
| Add/edit item | Maintain items and select an OMDb movie match | Managers |
| Inventory | Search, filter, and page through item/warehouse quantities | Staff and managers |
| Check-in/check-out | Preview and record stock movements | Staff and managers |
| Activity log | Review movement details and initiate corrections | Staff and managers; corrections are manager-only |
| Employee management | Create accounts, assign roles, deactivate access | Managers |
| Location management | Maintain and deactivate warehouses | Managers |

Primary navigation: **Sign in → Overview → Catalog / Inventory / Move Stock / Activity**. Managers also have access to administration.

## Agreed workflows

### Add a catalog item

1. A manager enters a name, item type, and media format.
2. Type and format may be edited until the first stock movement. After that, create a new catalog entry for another edition; name and optional metadata remain editable.
3. For a movie, the manager can search OMDb and select the correct title and release year.
4. The system fills available poster and age-rating information for review. Games use manual entry.
5. The manager saves the item. Creating an item adds no stock; balances begin at zero.
6. Employees add physical stock through check-in.

Posters and age ratings are optional. No match or an unavailable API must not prevent manual item creation. OMDb's `Rated` field is an age classification, not an IMDb review score. No usable rating or poster means the optional field remains empty.

### Check stock in or out

1. Select Check in or Check out, an active item, and an active warehouse.
2. Enter a positive whole-number quantity and optional notes.
3. Review the proposed resulting balance.
4. Confirm the movement.
5. The server validates current stock and saves the balance and movement history together, or saves neither.

The recorded employee comes from the signed-in account. The server supplies the timestamp. Check-out cannot exceed current stock, even if another employee changes the balance after the preview.

### Correct an error

A manager selects the mistaken transaction and records an opposite movement with a required explanation and a reference to the original. The original entry remains unchanged. Corrections also obey stock rules; an insufficient-stock correction cannot force a negative balance. Each entry can be fully reversed once; a correction may itself be reversed once, creating a chain. Partial corrections and duplicate reversals of the same entry are rejected. Archived items or inactive warehouses must be reactivated before recording a correction.

### Retire records and manage accounts

- Archive items or deactivate warehouses only when their stock is zero. An item's stock must be zero across all warehouses.
- Archived items and inactive warehouses cannot receive new movements. Historical references remain available.
- Managers create employee accounts and assign roles. No employee self-registration is provided.
- Inactive or terminated employees cannot access the application. Password resets and username changes revoke existing sessions on the next request. Saving your own employee account explicitly signs you out, with a warning and sign-in message.
- The first manager is created during setup. The last active manager cannot be deactivated or demoted.
- Login POSTs are limited to 10 per minute per client address on each application instance; GETs remain available. Unknown usernames still receive a password-hash verification. Session cookies require HTTPS.
- Passwords are stored using a supported salted password-hashing facility, never as plaintext. There is no forced first-login password change. Blank passwords on account edits preserve the existing hash; login-time hash upgrades cannot overwrite an intervening manager password reset.

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

## Current implementation and remaining work

- The instructor’s explicit `Program.Main` / `MapRoutes` foundation and parameterized ADO.NET helpers are in place. Razor Pages render the screens; no Entity Framework or extra Identity tables are used.
- The latest supplied five-table DDL already includes the required status, metadata, and correction fields. Milestone 2 requires no further migration on that aligned schema.
- Stock writes and linked corrections are atomic. Retirement takes an exclusive parent lock before checking stock. The dashboard uses one READ COMMITTED batch and derives total units from its warehouse rows; counts/recent movements can reflect later commits without a serializable read transaction.
- Each history entry can be fully reversed once, including correction entries. Corrections require active records, adequate stock, and a manager explanation.
- PR #5 was merged into main at `13b4f43`. Follow-up review fixes use the existing DDL and go through a separate owner-reviewed PR. Catalog, Inventory, and Manage Catalog use 100-row pages with filter-preserving navigation. Check the validation record and the current revision’s CI for test evidence.
- Continue the Azure portfolio deployment through separate branches/PRs, preserving the five-table business schema and application behavior. The guarded Azure installer adds four nonunique performance indexes, with an index-only upgrade for existing portfolio tables. Connection opening has bounded transient retries; commands are not replayed. Optional exact proxy addresses support trusted HTTPS/client forwarding. A manual Actions workflow checks main's successful CI, builds a portable Release package, and publishes to the existing F1 app using an environment-scoped publish-profile secret and pinned deploy action. The owner restricts the GitHub environment to main and removes the repository secret copy. A bounded HTTPS sign-in-page check verifies startup after upload; manual login verifies the database. Next steps are merging/configuring the workflow, deploying, confirming account-only starter data, and the live walkthrough. Deployment runs no schema or seed commands; the public link remains labeled launch pending until verified.
- Historical Windows/teacher-server and `.slnx` submission checks remain class-specific; they are not prerequisites for this portfolio transition. Live OMDb and fake-data screenshots remain pending for the public demo.

See the [requirements](requirements.md), [database notes](architecture-and-database.md), [roadmap](roadmap.md), [validation record](validation.md), and [submission guide](submission.md).
