# BlueBox Warehouse Inventory Manager

A browser-based internal inventory management project for a fictional movie and game rental business. BlueBox lets employees track warehouse quantities, record stock movements, and review who changed inventory and when.

**Status: both milestones’ planned application features are implemented.** The current phase is a web-only Azure portfolio deployment with the same inventory workflows and five-table design. The owner reports creating the Azure SQL database, running the schema installer, and creating the App Service website. Application publishing, live connectivity, demo accounts, OMDb verification, and screenshots remain pending. See the [Azure setup guide](docs/azure-portfolio-setup.md) and [validation record](docs/validation.md). Historical class verification is separate.

## Website demo — launch pending

**[Open BlueBox on Azure](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net)**

This is the portfolio website address. BlueBox has not yet been confirmed running there; until publishing is complete, the link may show Azure's default page or an unavailable application. Once launched, visitors will use the website in their browser without installing Visual Studio or SSMS. Demo access instructions will be added after fake data and accounts are verified.

The planned demo uses Azure App Service **F1 Free** and the **Azure SQL free offer**, subject to confirming the subscription settings. Free-tier idle shutdown, database resume, and usage limits can delay or temporarily interrupt access. GitHub hosts the source code; Azure runs the website and database.

## Project at a glance

| Area | Implementation |
| --- | --- |
| Purpose | Class project and portfolio demonstration |
| Interface | Browser-based internal employee website |
| Application | C# / .NET 10 with ASP.NET Core Razor Pages |
| Hosting | Azure App Service F1 Free; publishing and live verification pending |
| Database | Azure SQL for the portfolio, using the existing SQL Server-compatible schema |
| Data access | ADO.NET (`SqlConnection`, `SqlCommand`, `DataTable`), based on the instructor’s template |
| External integration | OMDb for optional movie posters and age classifications |
| Inventory model | Quantities per catalog item and warehouse location |
| Users | Staff and managers |

## Milestone 1 capabilities

- Employee sign-in with staff and manager permissions, secure cookies, and a bounded login-attempt limit.
- A searchable movie and game catalog, with separate entries for different formats. Type/format are fixed after the first movement to preserve edition history.
- Inventory quantities across warehouse locations; Catalog, Inventory, and Manage Catalog support 100-row pages with preserved filters.
- Check-in and check-out with a preview of the resulting balance.
- Protection against negative inventory and conflicting stock updates.
- Permanent movement history identifying the item, warehouse, employee, quantity, and time.
- Private demo-data seeding and first-manager setup commands.

## Milestone 2 capabilities

- Manager screens for catalog items, employee accounts, and warehouses.
- Last-active-manager protection, including competing account changes, and session revocation after password/username changes.
- Full opposite-movement corrections with explanations and permanent links; a correction can itself be corrected once.
- Zero-stock checks before item archival or warehouse deactivation.
- Server-side OMDb search and movie selection, optional posters/age ratings, and manual fallback.
- Overview totals, stock by warehouse, recent activity, and labeled Central time (or an explicit UTC fallback).

The [roadmap](docs/roadmap.md) distinguishes implemented features from final verification. The [Milestone 2 review guide](docs/milestone-2.md) describes manager workflows and correction rules.

**Scope:** warehouse stock only. Customer rentals, payments, late fees, streaming, and kiosk inventory are outside the initial project.

## Employee workflow

```mermaid
flowchart LR
    A[Employee signs in] --> O[Overview]
    O --> B[Warehouse inventory]
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
| [Azure portfolio setup](docs/azure-portfolio-setup.md) | Free tiers, browser database setup, private settings, SQL firewall rules, and launch checks |
| [GitHub Actions deployment](docs/github-actions-deployment.md) | Manual deployment from main to the existing Azure app, with no local .NET 10 installation |
| [Development and Windows setup](docs/development.md) | SDK, private connection configuration, demo data, running, and tests |
| [Project plan](docs/project-plan.md) | Agreed scope, screens, workflows, and decisions |
| [Requirements](docs/requirements.md) | Permissions, business rules, and acceptance criteria |
| [Architecture and database](docs/architecture-and-database.md) | Implemented application structure, entities, relationships, and schema alignment |
| [Roadmap](docs/roadmap.md) | Build sequence and completion criteria |
| [Demo guide](docs/demo-guide.md) | Fake demonstration data, presentation walkthrough, and screenshot plan |
| [SQL Server scripts](database/README.md) | When no migration is needed, older-baseline upgrades, and fresh-database setup |
| [Instructor template adaptation](docs/instructor-template.md) | Required structure, corrections, and review findings |
| [Runtime SQL query catalog](docs/sql-query-catalog.md) | Runtime SQL batches, with source links |
| [Milestone 2 review](docs/milestone-2.md) | Manager administration, correction chains, retirement, and review fixes |
| [PR #4 review resolution](docs/pr4-review-resolution.md) | Historical fixes delivered in merged PR #5 |
| [Main review resolution](docs/main-review-resolution.md) | Adopted and deferred suggestions, with no DDL changes |
| [PR #7 Azure review resolution](docs/pr7-review-resolution.md) | Portfolio indexes, safe proxy configuration, connection-open retry, and deployment checks |
| [Validation record](docs/validation.md) | Verified build/test results, CI links, and checks still pending |
| [Class submission and `.slnx`](docs/submission.md) | Pulling the merged code, converting the solution, and packaging source files |

Download the [Word version of the project plan](docs/BlueBox-Project-Plan.docx) for sharing or class submission. The Word copy is synchronized with the maintained Markdown plan; update both when project decisions change.

The supplied warehouse conceptual model, database design, data dictionary, and screen mockups informed this plan. [Source notes](docs/project-plan.md#planning-sources) explain how they relate to the original assignment brief. The original attachments are not bundled here.

## Running the project

To deploy the portfolio website, follow the [Azure process](docs/azure-portfolio-setup.md) and [GitHub Actions deployment guide](docs/github-actions-deployment.md). Add the existing app's Name as the `AZURE_WEBAPP_NAME` repository variable and its publish profile as the private `AZURE_WEBAPP_PUBLISH_PROFILE` Actions secret. After merging the workflow PR and waiting for main's build/SQL checks, select **Actions → Deploy BlueBox to Azure → Run workflow → main**. GitHub builds with the pinned SDK and publishes to the existing F1 website; a local .NET 10 installation is not needed for this deployment path.

Publishing creates no tables, accounts, items, stock, or history. The agreed portfolio starter data is one manager, four staff, and Fayetteville/Bentonville warehouses, created through the separately supplied private SQL file. Do not run `--seed-demo` for that setup; add catalog items and stock through the deployed application. Database connection settings and the OMDb key remain private in Azure. Connection opening has bounded retries for Azure resume/throttling; SQL commands are not automatically replayed.

For local development:

Install the SDK pinned in `global.json`, prepare the SQL Server tables, and configure `ConnectionStrings:BlueBox` privately. Follow the [development guide](docs/development.md) for Visual Studio setup and initial accounts, then run:

```sh
dotnet restore BlueBox.sln --locked-mode
dotnet build BlueBox.sln --no-restore
dotnet run --project src/BlueBox.Web
```

Open `BlueBox.sln` in a Visual Studio release supporting .NET 10 on Windows, or use VS Code/the CLI on Mac. No database is created, altered, or seeded automatically when the website starts. The teacher's database has not been accessed or changed from this workspace.

The connection string belongs in user secrets or an environment variable. Never commit credentials, real employee data, or API keys. Configure `Omdb:ApiKey` privately to enable movie lookup; manual entry works without a key.

## Validation

```sh
dotnet test BlueBox.sln --configuration Release
```

The default suite runs local unit/routing/provider checks and skips SQL Server groups when no disposable test instance is configured. SQL Server CI applies the supplied schema to generated disposable databases and runs real HTTP/stock/concurrency tests. The [validation record](docs/validation.md) distinguishes current results from historical baseline counts.

GitHub Actions runs on pull requests and pushes to `main`. A newer commit cancels an older run of the same PR; feature pushes no longer start duplicate push/PR jobs. Check the completed run for the current revision in [GitHub Actions](https://github.com/Smoofers-21/IMS-Bluebox/actions/workflows/build.yml) before merging a follow-up PR. The Windows walkthrough against the teacher’s SQL Server remains required before final class sign-off; automated create/drop tests belong only on disposable instances.

## Class submission

The repository and CI use `BlueBox.sln`. If the instructor requests `.slnx`, first pull the merged `main`, then run `dotnet sln BlueBox.sln migrate` and build `BlueBox.slnx`. Submit the solution with its project folders and required build/database files, following the [submission guide](docs/submission.md). The solution file alone does not contain the application code.

## Portfolio focus

The project demonstrates requirements analysis, workflow design, relational database modeling, employee authentication, role policy, transactional stock updates, and audit history. It also includes server-side external API integration, manager administration, and linked audit corrections. The website address is listed above; verified demo access and fake-data screenshots will follow launch checks.

The Azure portfolio uses a separate database with fictional records. It does not depend on access to the teacher's server. Original Windows/class and `.slnx` submission instructions remain available for the class version.
