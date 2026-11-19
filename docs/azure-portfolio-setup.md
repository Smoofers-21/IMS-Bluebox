# Azure portfolio setup

The portfolio transition keeps the C# application, five-table SQL Server design, permissions, stock transactions, corrections, and OMDb integration. Azure SQL replaces the teacher's database connection. Azure App Service will host the existing website; browsers use its HTTPS URL. SSMS is not required.

**Current status:** the owner reports a new, empty Azure SQL database. This workspace has not connected to it, installed tables, seeded data, or deployed a website. The steps below are preparation, not proof of a working deployment. Keep real server names, database credentials, connection strings, and OMDb keys outside the repository.

## 1. Confirm the free configuration

Use the Azure SQL **free offer** with **Auto-pause until next month** when the monthly allowance is exhausted. Do not enable paid overages. Confirm the offer and cost summary in the portal; a successful database deployment alone does not establish free-tier configuration. The free database can also pause while idle.

For the website, select an App Service **F1 (Free)** plan in an allowed region. Its shared CPU allowance and idle shutdown mean this is a limited portfolio demo. Use the included `azurewebsites.net` HTTPS address. Keep optional paid supporting services disabled. Check that the selected plan is F1 before creating it; a creation wizard can default to a paid tier.

## 2. Create tables in the empty cloud database

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
| `AllowedHosts` | The exact website hostname, without scheme or port |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Alternatively use `Database__Server`, `Database__Name`, `Database__Username`, and `Database__Password` separately and leave `ConnectionStrings__BlueBox` unset. The builder defaults to encryption with certificate validation. Do not turn off `Encrypt` or enable `TrustServerCertificate` to work around Azure configuration problems.

## 4. Initialize fake data once

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

## 5. Publish and verify the website

Keep the existing `net10.0` application and SDK from `global.json`. For a supported built-in .NET 10 App Service runtime, publish with:

```sh
dotnet publish src/BlueBox.Web/BlueBox.Web.csproj --configuration Release --output ./publish
```

Deploy the complete publish output through Visual Studio's Azure App Service publish flow or the host's supported deployment tooling. Choose the already-reviewed F1 website; do not let the publish wizard create a paid plan. Runtime selection and live publishing are still pending.

On the Azure SQL server, allow the website's documented outbound IPs through the firewall; browser users do not connect directly to SQL Server. Configure host HTTPS/proxy integration for the actual App Service environment and verify redirects, secure session cookies, and the observed client address used by login limiting. The current app does not trust arbitrary forwarded-IP headers. Do not add an unrestricted forwarding trust rule.

Use the [demo guide](demo-guide.md) to check staff/manager login, catalog and all pagers, item edits, check-in/out, insufficient stock, audit history, linked correction chains, retirement, self-edit sign-out, and OMDb/manual fallback. After the free database and website have idled, also test the first request: a paused database can need time to resume. Inspect private server logs for connection/resume errors before treating deployment as ready. Do not enable an always-running keep-alive job that consumes the free compute allowance.

Automated tests that create/drop databases continue to run only on disposable SQL Server instances in CI, never on this Azure portfolio database. Confirm actual cloud behavior separately, then add the working live URL and fake-data screenshots to the README. Passing local or disposable SQL tests alone does not prove Azure connectivity or free-tier settings.
