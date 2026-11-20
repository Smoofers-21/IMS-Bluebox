# Running and reviewing BlueBox

For the current web-only portfolio transition, follow [Azure portfolio setup](azure-portfolio-setup.md): the index-only upgrade for existing portfolio tables, private App Service settings, SQL outbound-IP firewall rules, publishing to the existing F1 website, fake-account initialization, and live checks. The [portfolio address](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net) is recorded but launch is pending. The instructions below also retain the original local/class setup.

Milestones 1 and 2 implement sign-in, catalog/warehouse browsing, ordinary stock movements, manager administration, linked correction chains, safe retirement, optional OMDb metadata, and the overview dashboard. Complete the verification steps below before final class/portfolio sign-off.

## Prerequisites

- .NET SDK **10.0.404**, pinned in `global.json` (newer patches in that feature band are allowed).
- On Windows: an up-to-date Visual Studio release supporting this .NET 10 SDK, with **ASP.NET and web development** installed. Older Visual Studio 2022 installations do not support .NET 10; use current Visual Studio or the CLI/VS Code.
- On Mac: VS Code with C# tooling, or the .NET CLI. The same solution runs on both operating systems.
- SQL Server access from the computer running the application. Verify the teacher’s server version in SSMS with `SELECT @@VERSION;` during the Windows walkthrough; SQL Server 2014 or newer is the assumed minimum for pooled connection isolation reset. CI verifies SQL Server 2022. An older target needs a compatibility review before use. SSMS alone does not establish access from a cloud workspace.

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

`RoutePrefix` is the starter's `/{username}` landing route; set it privately to the instructor-assigned route name if required. The assigned landing route sends signed-in users to `/Overview`. Application pages include `/Overview`, `/Inventory`, `/Catalog`, `/Activity`, and manager-only `/Admin` pages. Prefer the teacher's certificate trust configuration; the starter's certificate exception is not hardcoded into public source.

`BLUEBOX_PRIVATE_CONFIG` can point to a private JSON file outside the checkout; when set, the app loads that file explicitly. It is a local configuration option, not a file included in the repository or submission. On Windows, use Manage User Secrets or private environment variables. Build/unit checks need no teacher connection.

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

After setup, active managers use Manage → Employees to create/edit accounts or set a new password. A blank edit password preserves the hash. Password/username changes revoke existing sessions on their next request. Self-edits warn and explicitly sign out the current user. There is no public registration.

## 4. Build and run

From the repository root:

```sh
dotnet restore BlueBox.sln --locked-mode
dotnet build BlueBox.sln --no-restore
dotnet dev-certs https --trust
dotnet run --project src/BlueBox.Web
```

The development profile uses `https://localhost:7140` and `http://localhost:5140`, with HTTP redirecting to HTTPS. Visual Studio's startup button uses the same launch profile. Sign in with an active employee account.

The loopback URLs above are local development addresses, not a public portfolio deployment. Session cookies always require HTTPS. For a class demo accessed by a machine name, LAN address, or eventual public hostname, override `AllowedHosts` privately with the actual hostnames/IPs (no scheme or port):

```json
{
  "AllowedHosts": "localhost;127.0.0.1;YOUR_PC_NAME;YOUR_LAN_IP"
}
```

For example, put this alongside the connection settings in User Secrets or `BLUEBOX_PRIVATE_CONFIG`, or set `AllowedHosts` as an environment variable. This host allowlist does not bind a listener, open a firewall, establish HTTPS certificate trust, or publish the app. Configure those on the hosting machine when needed. For a verified reverse proxy not already handled by host integration, set `Hosting:KnownProxies` (`Hosting__KnownProxies` in App Service) to exact trusted proxy IPs separated by semicolons. The app processes one forwarding hop before HTTPS redirection and login limiting; untrusted peers cannot spoof the scheme/client address. Leave it unset for direct hosting and do not enable the unrestricted `ASPNETCORE_FORWARDEDHEADERS_ENABLED` shortcut. See [Azure setup](azure-portfolio-setup.md).

Login permits 10 POSTs per minute per client address on each app instance; excess attempts return 429 with `Retry-After`. GETs do not consume permits. Addresses are shared when requests reach the app through the same unconfigured proxy; this simple in-memory limit resets on restart and is suitable for the single-instance demo. Unknown usernames still run a password-hash check; this reduces the obvious fast path without promising constant-time responses.

Static files with a correct `asp-append-version` hash can be cached for a year; unversioned/mismatched URLs use a one-hour cache. Application pages, errors, and authenticated responses remain `no-store`.

## 5. Review the workflow

1. Sign in as staff; review Overview totals, then browse Catalog and Inventory. Use Previous/Next to reach all matches in pages of 100, preserving filters. Manage → Catalog items uses the same paging; submitting new filters starts at page 1.
2. Select a warehouse/item, check in a positive quantity, and compare the preview with the saved balance.
3. Check out available stock and find the attributed entry in Activity.
4. Attempt to check out too much; verify stock/history remain unchanged.
5. Try zero, negative, fractional, and excessive quantities; verify rejection.
6. Try signing in as the inactive employee.
7. In Activity, use Older/Newer to browse history in pages of 100; search and location/movement filters stay applied. Changing filters starts again at page 1.
8. In a disposable test database, exercise competing check-outs and confirm no overselling or lost history.

The overview uses one READ COMMITTED batch and derives its total units from the returned warehouse totals. Counts and recent movements can advance independently without a serializable dashboard read transaction.

Stock updates use a serializable SQL Server transaction and a conditional balance update. Log insertion and balance update commit together; a failure rolls both back. SQL Server deadlock victims receive a bounded whole-operation retry. Client-side previews are informational; the server rechecks access, record status, and stock.

Activity and overview display **Central time (CST/CDT)**, trying the IANA and Windows identifiers. If neither is available, values and captions explicitly show **UTC**. UTC remains in the database and HTML machine-readable timestamps. Follow the [Milestone 2 review](milestone-2.md) for manager/correction/OMDb checks.

## Optional OMDb setup

Store `Omdb:ApiKey` in Manage User Secrets (or `Omdb__ApiKey` as an environment variable). Never paste a working key into tracked files or browser code. A missing key does not prevent startup or manual item creation.

In Manage → Catalog items, choose Movie, enter a title, and select Search movies. Choose the correct title/year, review the returned name/poster URL/age rating, choose the format, and save. `Rated` is the age classification, not the IMDb review score. Games use manual fields. Unavailable posters/ratings remain empty. Poster URLs must use HTTPS; external images load directly in the browser with no referrer. The application never fetches arbitrary poster URLs on the server.

OMDb calls use a fixed HTTPS endpoint, a five-second timeout, no automatic retry, and disabled HTTP-client URL logging to keep the API key out of request logs. Provider errors show a manual-entry message. The tests use a fake provider and do not consume a real API key or quota.

## Automated checks

```sh
dotnet test BlueBox.sln --configuration Release
```

By default, tests run **unit and anonymous-routing checks** without database access. Real SQL Server integration tests are explicitly skipped when `BLUEBOX_TEST_SQLSERVER` is unset. There is no SQLite backend or Entity Framework dependency. The [validation record](validation.md) reports the current ADO.NET results.

For SQL Server validation, set `BLUEBOX_TEST_SQLSERVER` privately to a connection string for a **disposable test instance with permission to create databases**, then run the same command. The test harness connects to `master`, creates uniquely named `BlueBoxTest_<guid>` databases, applies the sanitized supplied current-schema reference, and drops only those generated databases afterward. **Do not use the teacher's shared server for this mode.** The database named in the input connection string is not used as the test database.

GitHub Actions runs both modes on pull requests and pushes to main. The disposable SQL Server 2022 CU28 image is pinned to a specific cumulative-update tag in the workflow; update it deliberately and re-run the SQL suite when upgrading. Newer commits cancel older runs within the same PR/ref; feature pushes no longer start duplicate push/PR runs. The SQL Server job creates a runner-local disposable instance and a generated password; it needs no teacher credentials. Its localhost-only certificate exception applies only to that disposable CI instance.

## Remaining verification

PR #5 was merged into main at `13b4f43`. See the [validation record](validation.md), [main-review decisions](main-review-resolution.md), and the current revision’s [GitHub Actions run](https://github.com/Smoofers-21/IMS-Bluebox/actions/workflows/build.yml). Earlier run/commit counts are historical evidence, not proof of subsequent code changes.

The remaining tasks are the Windows/teacher-database walkthrough, a live OMDb lookup with a private key, and final fake-data screenshots. Disposable SQL Server CI does not verify the teacher’s network, permissions, or certificate setup. No teacher database was accessed or modified from this workspace.

## Class submission

If the instructor requires `.slnx`, pull the merged `main` and follow the [submission guide](submission.md). It covers `dotnet sln BlueBox.sln migrate`, explicit `.slnx` build/test commands, the required project/build/database files, and ZIP verification. The repository and CI continue to use `BlueBox.sln`.

## Troubleshooting

- **503 / temporarily unavailable:** a SQL Server exception or timeout occurred; inspect private server logs for details.
- **500 / request could not be completed:** an unexpected application/configuration failure occurred. Inspect server logs; the browser does not receive raw exception details.
- **429 / too many sign-in attempts:** wait for the `Retry-After` interval before posting again; refreshing the sign-in form remains available.
- **400 from a machine-name/LAN URL:** verify the private `AllowedHosts` override as well as the host’s HTTPS/listener setup.
- **Invalid column name:** compare the connected database with the supplied current-schema reference. The latest teacher DDL is already aligned; use `001` only for an older baseline after reviewing its missing columns/constraints.
- **Unable to sign in:** check active status and a compatible password hash; a SQL row with a placeholder hash is not a usable login.
- **Unsupported SDK:** install the version pinned in `global.json` and update the editor if necessary.
- **No rows:** populate the empty demo database or provide existing fake records; the app does not seed automatically on startup.
