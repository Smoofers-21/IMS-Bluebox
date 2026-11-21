# Azure portfolio setup

The portfolio transition keeps the C# application, five-table SQL Server design, permissions, stock transactions, corrections, and OMDb integration. Azure SQL replaces the teacher's database connection. Azure App Service will host the existing website; browsers use its HTTPS URL. SSMS is not required.

**Current status, November 20, 2026:** the owner reports creating the Azure SQL database and website, running the schema and index update, and configuring app settings and SQL firewall rules. A manual GitHub Actions deployment workflow is prepared; its authentication setup, first deployment, live connectivity, and browser walkthrough remain pending. Account-only starter SQL has been supplied privately; its execution is not confirmed. This workspace has not connected to or changed Azure resources.

**Website address — launch pending:** [Open BlueBox on Azure](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net). Until the application is published and verified, the address may show Azure's default page or an unavailable application. The public hostname can be shared; keep database connection details, credentials, and OMDb keys private.

## 1. Confirm the free configuration

Use the Azure SQL **free offer** with **Auto-pause until next month** when the monthly allowance is exhausted. Do not enable paid overages. Confirm the offer and cost summary in the portal; a successful database deployment alone does not establish free-tier configuration. The free database can also pause while idle.

For the website, select an App Service **F1 (Free)** plan in an allowed region. Its shared CPU allowance and idle shutdown mean this is a limited portfolio demo. Use the included `azurewebsites.net` HTTPS address. Keep optional paid supporting services disabled. Check that the selected plan is F1 before creating it; a creation wizard can default to a paid tier.

For a new website, choose **Publish: Code**, **.NET 10 LTS**, and **Windows** for the planned host. Select the existing resource group and a region allowed by the subscription. Disable zone redundancy, Managed Instance, and optional paid monitoring; do not create a second database in the Web App wizard. Confirm **F1 Free / $0** on review. If F1 is unavailable, review region/subscription availability before proceeding with another plan. Publishing through Actions does not change the host runtime or pricing plan.

## 2. Create tables in the empty cloud database

The owner reports completing this step for the current portfolio database. For that database, confirm the five tables rather than rerunning the installer. The steps below are for a fresh installation.

1. Open the intended Azure SQL database and select **Query editor**. Sign in using the database authentication configured during creation.
2. If access is blocked, allow the exact client IP shown by the portal through the SQL server firewall. School/tenant policies or missing permissions require the subscription administrator's help.
3. Open [001_create_portfolio_schema.sql](../database/azure/001_create_portfolio_schema.sql).
4. Replace `YourAzureDatabaseName` in the declaration with the exact name of the selected database. Run the whole script together.
5. Confirm its success result and the five tables. The tables are intentionally empty; do not rerun it to add sample records.

The script has no `USE`, `GO`, `CREATE DATABASE`, or automatic migration. It checks the target name, rejects existing BlueBox objects, and creates the supplied definitions in a single transaction. A DDL error rolls that transaction back. Columns, keys, defaults, and constraints match the checked-in [current schema](../database/reference/current-schema.sql). Four additional nonunique portfolio indexes support correction/history and warehouse stock lookups; the business schema stays the same.

**For the current database, where the tables already exist:** run [002_add_portfolio_indexes.sql](../database/azure/002_add_portfolio_indexes.sql) in Query editor after replacing its expected database name. It adds missing indexes in one transaction, retains existing data, and can be rerun. Do not reinstall or drop tables. The script leaves existing same-named indexes alone; inspect any manually modified definitions first. This is a portfolio upgrade, not a teacher-database instruction.

## 3. Configure the application privately

The connection-string format the application accepts is:

```text
Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DATABASE;Persist Security Info=False;User ID=YOUR_SQL_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;
```

Replace the `YOUR_…` placeholders privately. If a password contains connection-string delimiters such as semicolons, use the existing `Database` configuration fields so the app's `SqlConnectionStringBuilder` quotes them correctly. Both the builder and this example use a 60-second connection timeout to allow database resume. Existing complete connection strings retain their own timeout and need editing privately to change it.

For a local setup using Visual Studio, put `ConnectionStrings:BlueBox` in **BlueBox.Web → Manage User Secrets**. An existing `BLUEBOX_PRIVATE_CONFIG` file loads last; select only the intended portfolio settings or unset that variable to prevent it overriding User Secrets. Never use the teacher's saved connection for portfolio setup.

For App Service, set these under **Settings → Environment variables → App settings**:

| Name | Private value |
| --- | --- |
| `ConnectionStrings__BlueBox` | The complete portfolio database connection string |
| `Omdb__ApiKey` | The existing OMDb key, if using movie lookup |
| `AllowedHosts` | `bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Add each setting individually and leave **Deployment slot setting** unchecked for this single production F1 app. The double underscores are intentional: they map the environment variables to nested application settings. Select **Apply/Save** after adding them. `Omdb__ApiKey` contains only the key, without an API URL or query parameters. `AllowedHosts` contains no `https://` prefix or path; update it if the website hostname changes.

Alternatively use `Database__Server`, `Database__Name`, `Database__Username`, and `Database__Password` separately and leave `ConnectionStrings__BlueBox` unset. The builder defaults to encryption with certificate validation. Do not turn off `Encrypt` or enable `TrustServerCertificate` to work around Azure configuration problems.

**Proxy configuration, only if the host needs it:** Windows App Service's IIS integration may already restore HTTPS and the client address. Verify the actual behavior first. For an additional verified proxy hop, set `Hosting__KnownProxies` to the exact connecting proxy IPs separated by semicolons. Obtain these from the host's trusted network configuration/private diagnostics; the website's public inbound/outbound IPs are not a substitute. The app accepts forwarded client IP and scheme only from those addresses, with a one-hop limit. Leave the setting unset for direct local/teacher hosting.

Do **not** set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: that framework shortcut clears trusted-peer restrictions. Merely detecting App Service does not authenticate headers. See the [review resolution](pr7-review-resolution.md) for the implementation decision. A host with an unresolved proxy chain is not ready for public launch.

## 4. Allow App Service through the SQL firewall

1. In the **Web App → Properties**, find **Outbound IP addresses**. Review **Additional outbound IP addresses** as well when preparing for host changes.
2. Open the Azure **SQL server → Networking → Firewall rules**. Allow each required outbound IP individually, using the same address for the rule's start and end IP, and save.
3. A local setup/seeding command needs its own exact client IP allowed too. Remove temporary client rules when they are no longer needed. Recheck the website's outbound IPs after host or plan changes.

Browser visitors do not need SQL firewall rules; their browsers connect only to App Service over HTTPS. The website's outbound addresses differ from its inbound address. Use these explicit rules rather than a broad allow-all-Azure-services rule. F1 uses public connectivity; this process does not require paid private endpoints or VNet integration.

## 5. Publish BlueBox to the existing website

Publishing uploads the application; it does not create tables or accounts. Keep the existing `net10.0` application and SDK from `global.json`.

**Current deployment path: GitHub Actions.** Follow [GitHub Actions deployment](github-actions-deployment.md) to add the existing app name and private publish-profile secret. After merging the workflow PR and waiting for main's build/SQL CI run to succeed, choose **Actions → Deploy BlueBox to Azure → Run workflow → main**. GitHub builds and packages the selected commit, then publishes it to the existing app. Your local Visual Studio does not need .NET 10 for this path; the Azure host does.

**Alternative: Visual Studio on Windows**, if a compatible local SDK/editor is installed:

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

Deploy the complete publish output through the host's supported deployment tooling. Repository source uploads alone are not a substitute for a completed build/publish. The existing build workflow tests PRs/main; the separate deployment workflow runs only when manually selected. Merging a GitHub PR never starts a deployment automatically.

## 6. Initialize fake data once

The agreed portfolio setup is **one active manager, four active staff, and two warehouses in Fayetteville, AR and Bentonville, AR**. Catalog items, current inventory, and transaction history start empty. Verify the selected database in Azure Query editor:

```sql
SELECT DB_NAME() AS selected_database;
```

Run the separately supplied **private account/location starter SQL** once in the intended Azure database if it has not already been run. That file checks the target and requires all five tables to be empty before inserting employees and locations in one transaction. It contains app-compatible password hashes; the separate private login file contains distinct initial passwords. Neither file belongs in GitHub. No database password is needed in the SQL file.

**Do not run `--seed-demo` for this portfolio setup.** The older class/test seeder also creates catalog items, balances, and opening movements. It remains available for disposable test/class demonstrations, documented in [local development](development.md#3-choose-initial-data). After launch, the owner will add catalog entries and stock through the application.

Ordinary startup and Actions deployment do not apply schema or seed records. If tables already contain data, do not delete records to rerun setup; use the existing manager administration, or review the [first-manager procedure](development.md#tables-already-contain-data-but-no-active-manager) if no active manager exists. Share only intended staff demo access after verification; keep manager passwords and SQL credentials private.

## 7. Verify the public demo before announcing launch

Verify the host's HTTPS/client-address forwarding using the private configuration above. Confirm the HTTPS login page has no redirect loop, cookies remain secure, and eleven quick invalid login submissions from one client reach 429 while a client on a different public IP can still submit. Devices sharing a public IP intentionally share a bucket. Do not test by repeatedly submitting real credentials or by trusting arbitrary forwarded headers.

For deployment diagnosis on the planned Windows host, open **App Service → Monitoring → App Service logs**, temporarily enable **Application logging (Filesystem)**, then use **Log stream** to inspect startup/connection errors. Keep logs private and turn verbose logging off afterward. This does not require Application Insights, paid storage logging, or a monitoring upgrade.

Use the [demo guide](demo-guide.md) to check staff/manager login, catalog and all pagers, item edits, check-in/out, insufficient stock, audit history, linked correction chains, retirement, self-edit sign-out, and OMDb/manual fallback. After the free database and website have idled, also test the first request: a paused database can need time to resume. Inspect private server logs for connection/resume errors before treating deployment as ready. Do not enable an always-running keep-alive job that consumes the free compute allowance.

Connection opening retries up to three total attempts for explicit Azure availability/throttling failures, with bounded exponential delays and cancellation support. SQL commands and ambiguous writes are never replayed by this policy. Resume can still exceed the retry budget or fail when the free allowance is exhausted; the existing 503 response remains the fallback.

Automated tests that create/drop databases continue to run only on disposable SQL Server instances in CI, never on this Azure portfolio database. After the live walkthrough, update the README's launch status, add verified staff access instructions, and capture fake-data screenshots. Keep the manager password private. Passing local or disposable SQL tests alone does not prove Azure connectivity or free-tier settings.
