# BlueBox Warehouse Inventory Manager

A browser-based internal inventory management project for a fictional movie and game rental business. BlueBox lets employees track warehouse quantities, record stock movements, and review who changed inventory and when.

**Status: Milestone 1 implemented; SQL Server and Windows/teacher-database review pending.** Sign-in, read-only catalog, inventory, stock movements, and activity history are implemented. Automated tests run against a temporary relational test database; SQL Server verification is a separate required check. Manager administration, corrections, OMDb integration, and the dashboard are Milestone 2 work.

## Project at a glance

| Area | Plan |
| --- | --- |
| Purpose | Class project and portfolio demonstration |
| Interface | Browser-based internal employee website |
| Application | C# / .NET 10 with ASP.NET Core Razor Pages |
| Database | Microsoft SQL Server, managed through SQL Server Management Studio (SSMS) |
| Data access | Entity Framework Core, mapped to the existing five SQL Server tables |
| External integration | OMDb for movie posters and age classifications (Milestone 2) |
| Inventory model | Quantities per catalog item and warehouse location |
| Users | Staff and managers |

## Milestone 1 capabilities

- Employee sign-in with staff and manager permissions.
- A searchable movie and game catalog, with separate entries for different formats.
- Inventory quantities across warehouse locations.
- Check-in and check-out with a preview of the resulting balance.
- Protection against negative inventory and conflicting stock updates.
- Permanent movement history identifying the item, warehouse, employee, quantity, and time.
- Private demo-data seeding and first-manager setup commands.

Milestone 2 adds manager corrections and administration, OMDb movie lookup, and the overview dashboard. The [roadmap](docs/roadmap.md) tracks both milestones and verification still needed.

**Scope:** warehouse stock only. Customer rentals, payments, late fees, streaming, and kiosk inventory are outside the initial project.

## Employee workflow

```mermaid
flowchart LR
    A[Employee signs in] --> B[Warehouse inventory]
    B --> C[Find item and warehouse]
    C --> D[Choose check-in or check-out]
    D --> E[Enter quantity and preview balance]
    E --> F{Valid and enough stock?}
    F -->|Yes| G[Save balance and history together]
    F -->|No| H[Explain the issue; save nothing]
    G --> I[Review activity]
```

Check-in means adding units to a warehouse; check-out means removing units from it. These are warehouse movements, not customer rental transactions.

## Documentation

| Document | Contents |
| --- | --- |
| [Development and Windows setup](docs/development.md) | SDK, private connection configuration, demo data, running, and tests |
| [Project plan](docs/project-plan.md) | Agreed scope, screens, workflows, and decisions |
| [Requirements](docs/requirements.md) | Permissions, business rules, and acceptance criteria |
| [Architecture and database](docs/architecture-and-database.md) | Proposed application structure, entities, relationships, and schema adjustments |
| [Roadmap](docs/roadmap.md) | Build sequence and completion criteria |
| [Demo guide](docs/demo-guide.md) | Planned fake data and a portfolio presentation walkthrough |
| [SQL Server scripts](database/README.md) | Existing-table schema update and SSMS instructions |

Download the [Word version of the project plan](docs/BlueBox-Project-Plan.docx) for sharing or class submission. The Markdown plan is the maintained source; regenerate the Word copy when the baseline changes.

The supplied warehouse conceptual model, database design, data dictionary, and screen mockups informed this plan. [Source notes](docs/project-plan.md#planning-sources) explain how they relate to the original assignment brief. The original attachments are not bundled here.

## Running the project

Install the SDK pinned in `global.json`, prepare the SQL Server tables, and configure `ConnectionStrings:BlueBox` privately. Follow the [development guide](docs/development.md) for Visual Studio setup and initial accounts, then run:

```sh
dotnet restore BlueBox.sln --locked-mode
dotnet build BlueBox.sln --no-restore
dotnet run --project src/BlueBox.Web
```

Open `BlueBox.sln` in a Visual Studio release supporting .NET 10 on Windows, or use VS Code/the CLI on Mac. No database is created, altered, or seeded automatically when the website starts. The teacher's database has not been accessed or changed from this workspace.

The connection string belongs in user secrets or an environment variable. Never commit credentials, real employee data, or API keys. OMDb is not called in Milestone 1.

## Validation

```sh
dotnet test BlueBox.sln --configuration Release
```

The default suite runs the actual Razor Pages application and stock service against disposable SQLite relational databases; SQLite is only a test dependency. SQL Server mode applies the actual baseline/update scripts to generated test databases on a disposable instance. GitHub Actions is configured for both modes. See [test setup and limits](docs/development.md#automated-checks).

The Windows walkthrough against the teacher's SQL Server remains required before calling the milestone complete. Do not use the shared teacher server for automated tests that create/drop test databases.

## Portfolio focus

The project demonstrates relational database design, employee authentication, role policy, transactional stock updates, and audit history in C#. External API integration is scheduled for Milestone 2. Verified SQL Server results, screenshots, and a public demo link will be added as those capabilities are validated and deployed.
