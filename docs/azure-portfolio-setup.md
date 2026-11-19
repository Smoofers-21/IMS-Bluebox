# Azure portfolio setup

The portfolio transition keeps the C# application, five-table SQL Server design, permissions, stock transactions, corrections, and OMDb integration. Azure SQL replaces the teacher's database connection. Azure App Service will host the existing website; browsers use its HTTPS URL. SSMS is not required.

**Current status, November 19, 2026:** the owner reports creating the Azure SQL database, running the fresh-schema query, and creating the App Service website. Publishing BlueBox, testing live connectivity, initializing fake accounts, and completing the browser walkthrough remain pending. This workspace has not connected to or changed Azure resources.

**Website address — launch pending:** [Open BlueBox on Azure](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net). Until the application is published and verified, the address may show Azure's default page or an unavailable application. The public hostname can be shared; keep database connection details, credentials, and OMDb keys private.

## 1. Confirm the free configuration

Use the Azure SQL **free offer** with **Auto-pause until next month** when the monthly allowance is exhausted. Do not enable paid overages. Confirm the offer and cost summary in the portal; a successful database deployment alone does not establish free-tier configuration. The free database can also pause while idle.

For the website, select an App Service **F1 (Free)** plan in an allowed region. Its shared CPU allowance and idle shutdown mean this is a limited portfolio demo. Use the included `azurewebsites.net` HTTPS address. Keep optional paid supporting services disabled. Check that the selected plan is F1 before creating it; a creation wizard can default to a paid tier.

For a new website, choose **Publish: Code**, **.NET 10 LTS**, and **Windows** for the planned Visual Studio publishing flow. Select the existing resource group and a region allowed by the subscription. Disable zone redundancy, Managed Instance, and optional paid monitoring; do not create a second database in the Web App wizard. Confirm **F1 Free / $0** on review. If F1 is unavailable, review region/subscription availability before proceeding with another plan.

## 2. Create tables in the empty cloud database

The owner reports completing this step for the current portfolio database. For that database, confirm the five tables rather than rerunning the installer. The steps below are for a fresh installation.

1. Open the intended Azure SQL database and select **Query editor**. Sign in using the database authentication configured during creation.
2. If access is blocked, allow the exact client IP shown by the portal through the SQL server firewall. School/tenant policies or missing permissions require the subscription administrator's help.
3. Open [001_create_portfolio_schema.sql](../database/azure/001_create_portfolio_schema.sql).
4. Replace `YourAzureDatabaseName` in the declaration with the exact name of the selected database. Run the whole script together.
5. Confirm its success result and the five tables. The tables are intentionally empty; do not rerun it to add sample records.

The script has no `USE`, `GO`, `CREATE DATABASE`, or automatic migration. It checks the target name, rejects existing BlueBox objects, and creates the supplied definitions in a single transaction. A DDL error rolls that transaction back. Columns, keys, defaults, and constraints match the checked-in [current schema](../database/reference/current-schema.sql); this is a fresh-install wrapper, not a new business schema or teacher-database upgrade.

## 3. Configure the application privately

The connection-string format the application accepts is:

```text
Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DATABASE;Persist Security Info=False;User ID=YOUR_SQL_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Replace placeholders privately. Do not include the placeholder braces in the actual password. If a password contains connection-string delimiters such as semicolons, use the existing `Database` configuration fields so the app's `SqlConnectionStringBuilder` quotes them correctly.

For a local setup using Visual Studio, put `ConnectionStrings:BlueBox` in **BlueBox.Web → Manage User Secrets**. An existing `BLUEBOX_PRIVATE_CONFIG` file loads last; select only the intended portfolio settings or unset that variable to prevent it overriding User Secrets. Never use the teacher's saved connection for portfolio setup.

For App Service, set these under **Settings → Environment variables → App settings**:

| Name | Private value |
| --- | --- |
| `ConnectionStrings__BlueBox` | The complete portfolio database connection string |
| `Omdb__ApiKey` | The existing OMDb key, if using movie lookup |
| `AllowedHosts` | `bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

The double underscores are intentional: they map the environment variables to nested application settings. Select **Apply/Save** after adding them. `Omdb__ApiKey` contains only the key, without an API URL or query parameters. `AllowedHosts` contains no `https://` prefix or path; update it if the website hostname changes.

Alternatively use `Database__Server`, `Database__Name`, `Database__Username`, and `Database__Password` separately and leave `ConnectionStrings__BlueBox` unset. The builder defaults to encryption with certificate validation. Do not turn off `Encrypt` or enable `TrustServerCertificate` to work around Azure configuration problems.

## 4. Allow App Service through the SQL firewall

1. In the **Web App → Properties**, find **Outbound IP addresses**. Review **Additional outbound IP addresses** as well when preparing for host changes.
2. Open the Azure **SQL server → Networking → Firewall rules**. Allow each required outbound IP individually, using the same address for the rule's start and end IP, and save.
3. A local setup/seeding command needs its own exact client IP allowed too. Remove temporary client rules when they are no longer needed. Recheck the website's outbound IPs after host or plan changes.

Browser visitors do not need SQL firewall rules; their browsers connect only to App Service over HTTPS. The website's outbound addresses differ from its inbound address. Use these explicit rules rather than a broad allow-all-Azure-services rule. F1 uses public connectivity; this process does not require paid private endpoints or VNet integration.

## 5. Publish BlueBox to the existing website

Publishing uploads the application; it does not create tables or accounts. Keep the existing `net10.0` application and SDK from `global.json`. In Visual Studio on Windows:

1. After the owner merges the relevant PR, pull the latest `main` and open `BlueBox.sln`. Install the pinned .NET SDK if needed.
2. Right-click **BlueBox.Web → Publish → Azure → Azure App Service**. Select the operating system used by the existing website; the planned host is Windows.
3. Select the existing portfolio web app and verify its subscription/resource group. Do not create a replacement website or paid plan in the publish wizard.
4. Publish with **Release** configuration and the existing .NET 10 runtime. Keep the App Service plan at **F1 Free**.
5. Open the HTTPS website address above. Confirm the BlueBox sign-in screen replaces Azure's default page. Login still requires the account initialization below.

The equivalent CLI build for supported host deployment tooling is:

```sh
dotnet restore BlueBox.sln --locked-mode
dotnet publish src/BlueBox.Web/BlueBox.Web.csproj --configuration Release --no-restore --output ./publish
```

Deploy the complete publish output through the host's supported deployment tooling. Repository source uploads alone are not a substitute for a completed build/publish. Azure deployment is separate from merging a GitHub PR; the repository's current Actions workflow tests the application and does not deploy it.

## 6. Initialize fake data once

Use a privately configured local checkout to run the existing explicit setup command against the new cloud database. This does not require SSMS. Verify the selected connection first with this read-only query in Azure Query editor:

```sql
SELECT DB_NAME() AS selected_database;
```

Ensure the local application's private connection selects that same database. Set `Demo:Password` privately to a password of 12–256 characters, then run:

```sh
dotnet restore BlueBox.sln --locked-mode
dotnet run --project src/BlueBox.Web -- --seed-demo
```

The seeder requires all five tables to be empty. It creates fictional warehouses, catalog items, opening stock/history, `jordan.lee` (manager), `avery.chen` (staff), and `sam.rivera` (inactive). Remove `Demo:Password` after setup. It does not run automatically on website startup. Existing tables with records need the separate [first-manager procedure](development.md#tables-already-contain-data-but-no-active-manager), not reseeding.

For public demo access, share only the staff account's app login. Before sharing it, use manager administration to give the manager a different private password: the seeder initially uses the same password for both active accounts. Do not publish the manager password or SQL credentials. Staff can change fictional inventory through normal movements; this is an interactive demo.

## 7. Verify the public demo before announcing launch

Configure host HTTPS/proxy integration for the actual App Service environment and verify redirects, secure session cookies, and the observed client address used by login limiting. The current app does not trust arbitrary forwarded-IP headers. Do not add an unrestricted forwarding trust rule.

Use the [demo guide](demo-guide.md) to check staff/manager login, catalog and all pagers, item edits, check-in/out, insufficient stock, audit history, linked correction chains, retirement, self-edit sign-out, and OMDb/manual fallback. After the free database and website have idled, also test the first request: a paused database can need time to resume. Inspect private server logs for connection/resume errors before treating deployment as ready. Do not enable an always-running keep-alive job that consumes the free compute allowance.

Automated tests that create/drop databases continue to run only on disposable SQL Server instances in CI, never on this Azure portfolio database. After the live walkthrough, update the README's launch status, add verified staff access instructions, and capture fake-data screenshots. Keep the manager password private. Passing local or disposable SQL tests alone does not prove Azure connectivity or free-tier settings.
