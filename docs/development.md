# Running and reviewing BlueBox

Milestones 1 and 2 implement sign-in, catalog/warehouse browsing, ordinary stock movements, manager administration, linked correction chains, safe retirement, optional OMDb metadata, and the overview dashboard. Complete the verification steps below before final class/portfolio sign-off.

## Prerequisites

- .NET SDK **10.0.404**, pinned in `global.json` (newer patches in that feature band are allowed).
- On Windows: an up-to-date Visual Studio release supporting this .NET 10 SDK, with **ASP.NET and web development** installed. Older Visual Studio 2022 installations do not support .NET 10; use current Visual Studio or the CLI/VS Code.
- On Mac: VS Code with C# tooling, or the .NET CLI. The same solution runs on both operating systems.
- SQL Server access from the computer running the application. SSMS alone does not establish access from a cloud workspace.

Open `BlueBox.sln` in Visual Studio. Set **BlueBox.Web** as the startup project. The application targets the existing five-table schema; it never creates or migrates the teacher's database automatically.

## 1. Prepare the database

The latest supplied DDL already has the planned columns and constraints, so **do not rerun the one-time migration on that database**. Follow [database instructions](../database/README.md) when preparing a different, empty database. For existing baseline tables, apply `001_align_warehouse_schema.sql` once. For a new empty development database, apply `000` and then `001`. Edit each script's expected database name before running it in SSMS.

The table names are `dbo.EMPLOYEE`, `dbo.ITEM`, `dbo.LOCATION`, `dbo.CURRENT_INVENTORY`, and `dbo.TRANSACTION_LOG`.

## 2. Set a private connection string

In Visual Studio, right-click **BlueBox.Web → Manage User Secrets**. Use a local JSON configuration like the following, replacing the placeholders privately:

```json
{
  "ConnectionStrings": {
    "BlueBox": "Server=YOUR_SERVER;Database=YOUR_DATABASE;User ID=YOUR_USERNAME;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False"
  }
}
```

For Windows authentication, use `Integrated Security=True` instead of SQL username/password, if that is how your teacher configured access. If certificate validation fails, obtain the appropriate certificate/trust configuration from the teacher. Certificate validation is enabled by default; if the teacher explicitly requires a different setting, it belongs in private configuration.

Alternatively, configure `ConnectionStrings__BlueBox` as an environment variable. User secrets are loaded in the Development environment. Do not put credentials in `appsettings.json`, `launchSettings.json`, SQL scripts, or GitHub.


### Instructor-template configuration fields

Instead of a connection string, you can set the starter template's four fields in user secrets:

```json
{
  "Database": {
    "Server": "YOUR_SERVER",
    "Name": "YOUR_DATABASE",
    "Username": "YOUR_SQL_USERNAME",
    "Password": "YOUR_PASSWORD",
    "RoutePrefix": "bluebox",
    "TrustServerCertificate": false
  }
}
```

`RoutePrefix` is the starter's `/{username}` landing route; set it privately to the instructor-assigned route name if required. Application pages remain at `/Inventory`, `/Catalog`, and the other documented routes. Prefer the teacher's certificate trust configuration; the starter's certificate exception is not hardcoded into public source.

For this workspace, the supplied values were saved outside the checkout in a permission-restricted private file. They are not pushed to GitHub or loaded by default. `BLUEBOX_PRIVATE_CONFIG` can point to such a file when deliberately running locally. On your Windows PC, use Manage User Secrets with your own private values. No teacher connection is required for build/unit checks.

## 3. Choose initial data

### Empty demo tables

Add `Demo:Password` to user secrets with a password of 12–256 characters, then run from the repository root:

```sh
dotnet run --project src/BlueBox.Web -- --seed-demo
```

This adds fictional warehouses/items and these accounts, all using the privately configured demo password:

| Username | Role/status |
| --- | --- |
| jordan.lee | Active manager |
| avery.chen | Active staff |
| sam.rivera | Inactive staff; cannot sign in |

Opening quantities are supported by matching check-in history. The seeder requires **all five tables to be empty** and refuses to overwrite any existing data. It does not create the database or tables. Remove `Demo:Password` from secrets after seeding.

### Tables already contain data, but no active manager

Configure `Bootstrap:Username` and `Bootstrap:Password` privately (password 12–256 characters), then run:

```sh
dotnet run --project src/BlueBox.Web -- --create-manager
```

This adds the first manager only; it refuses when an active manager already exists or the requested username is taken. It does not change existing passwords, stock, or history. Remove the Bootstrap values afterward. Existing employee password hashes must be compatible with ASP.NET Core `PasswordHasher<Employee>`; placeholder/plaintext values cannot sign in.

After setup, active managers use Manage → Employees to create/edit accounts or set a new password. Existing passwords remain unchanged when the password field is blank. There is no public registration.

## 4. Build and run

From the repository root:

```sh
dotnet restore BlueBox.sln --locked-mode
dotnet build BlueBox.sln --no-restore
dotnet dev-certs https --trust
dotnet run --project src/BlueBox.Web
```

The development profile uses `https://localhost:7140` and `http://localhost:5140`, with HTTP redirecting to HTTPS. Visual Studio's startup button uses the same launch profile. Sign in with an active employee account.

The loopback URLs above are local development addresses, not a public portfolio deployment. Set `AllowedHosts` appropriately before hosting publicly; it currently allows localhost only.

## 5. Review the workflow

1. Sign in as staff; review Overview totals, then browse Catalog and Inventory.
2. Select a warehouse/item, check in a positive quantity, and compare the preview with the saved balance.
3. Check out available stock and find the attributed entry in Activity.
4. Attempt to check out too much; verify stock/history remain unchanged.
5. Try zero, negative, fractional, and excessive quantities; verify rejection.
6. Try signing in as the inactive employee.
7. In Activity, use Older/Newer to browse history in pages of 100; search and location/movement filters stay applied. Changing filters starts again at page 1.
8. In a disposable test database, exercise competing check-outs and confirm no overselling or lost history.

The overview reads all sections in one serializable transaction and one database round trip, so its stock totals remain consistent during concurrent movements.

Stock updates use a serializable SQL Server transaction and a conditional balance update. Log insertion and balance update commit together; a failure rolls both back. SQL Server deadlock victims receive a bounded whole-operation retry. Client-side previews are informational; the server rechecks access, record status, and stock.

Activity and overview display **Central time (CST/CDT)**, with UTC retained in the database and HTML machine-readable timestamps. Follow the [Milestone 2 review](milestone-2.md) for manager/correction/OMDb checks.

## Optional OMDb setup

Store `Omdb:ApiKey` in Manage User Secrets (or `Omdb__ApiKey` as an environment variable). Never paste a working key into tracked files or browser code. A missing key does not prevent startup or manual item creation.

In Manage → Catalog items, choose Movie, enter a title, and select Search movies. Choose the correct title/year, review the returned name/poster URL/age rating, choose the format, and save. `Rated` is the age classification, not the IMDb review score. Games use manual fields. Unavailable posters/ratings remain empty. Poster URLs must use HTTPS; external images load directly in the browser with no referrer. The application never fetches arbitrary poster URLs on the server.

OMDb calls use a fixed HTTPS endpoint, a five-second timeout, no automatic retry, and disabled HTTP-client URL logging to keep the API key out of request logs. Provider errors show a manual-entry message. The tests use a fake provider and do not consume a real API key or quota.

## Automated checks

```sh
dotnet test BlueBox.sln --configuration Release
```

By default, tests run **unit and anonymous-routing checks** without database access. Real SQL Server integration tests are explicitly skipped when `BLUEBOX_TEST_SQLSERVER` is unset. There is no SQLite backend or Entity Framework dependency. The earlier 27-test SQLite result does not validate this ADO.NET refactor.

For SQL Server validation, set `BLUEBOX_TEST_SQLSERVER` privately to a connection string for a **disposable test instance with permission to create databases**, then run the same command. The test harness connects to `master`, creates uniquely named `BlueBoxTest_<guid>` databases, applies the sanitized supplied current-schema reference, and drops only those generated databases afterward. **Do not use the teacher's shared server for this mode.** The database named in the input connection string is not used as the test database.

GitHub Actions runs both modes for feature-branch pushes, pull requests, and main; concurrent duplicate builds for the same branch are canceled. The SQL Server job creates a runner-local disposable instance and a generated password; it needs no teacher credentials. Its localhost-only certificate exception applies only to that disposable CI instance.

## Remaining verification

Local unit/routing test results do not prove the teacher's server is reachable or SQL Server locking behaves identically. Milestone 1 passed [its SQL Server CI run](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44132304808). Verify the new Milestone 2 run and complete the Windows/teacher-database walkthrough for final sign-off. No remote database was modified by preparing this application.

## Troubleshooting

- **503 / temporarily unavailable:** inspect server-side logs for connection/schema errors. The browser intentionally does not receive raw database exceptions.
- **Invalid column name:** confirm `001` was applied to the exact database in the connection string.
- **Unable to sign in:** check active status and a compatible password hash; a SQL row with a placeholder hash is not a usable login.
- **Unsupported SDK:** install the version pinned in `global.json` and update the editor if necessary.
- **No rows:** populate the empty demo database or provide existing fake records; the app does not seed automatically on startup.
