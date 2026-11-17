# Running and reviewing Milestone 1

Milestone 1 implements employee sign-in, read-only catalog/details, warehouse inventory, check-in/check-out with a live balance preview, and permanent activity history. Manager editing, corrections, OMDb, and the overview dashboard remain Milestone 2 work.

## Prerequisites

- .NET SDK **10.0.404**, pinned in `global.json` (newer patches in that feature band are allowed).
- On Windows: an up-to-date Visual Studio release supporting this .NET 10 SDK, with **ASP.NET and web development** installed. Older Visual Studio 2022 installations do not support .NET 10; use current Visual Studio or the CLI/VS Code.
- On Mac: VS Code with C# tooling, or the .NET CLI. The same solution runs on both operating systems.
- SQL Server access from the computer running the application. SSMS alone does not establish access from a cloud workspace.

Open `BlueBox.sln` in Visual Studio. Set **BlueBox.Web** as the startup project. The application targets the existing five-table schema; it never creates or migrates the teacher's database automatically.

## 1. Prepare the database

Follow [database instructions](../database/README.md). For existing baseline tables, apply `001_align_warehouse_schema.sql` once. For a new empty development database, apply `000` and then `001`. Edit each script's expected database name before running it in SSMS.

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

For Windows authentication, use `Integrated Security=True` instead of SQL username/password, if that is how your teacher configured access. If certificate validation fails, obtain the appropriate certificate/trust configuration from the teacher. The application does not disable certificate verification for the teacher's server.

Alternatively, configure `ConnectionStrings__BlueBox` as an environment variable. User secrets are loaded in the Development environment. Do not put credentials in `appsettings.json`, `launchSettings.json`, SQL scripts, or GitHub.

## 3. Choose initial data

### Empty demo tables

Add `Demo:Password` to user secrets with a password of at least 12 characters, then run from the repository root:

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

Configure `Bootstrap:Username` and `Bootstrap:Password` privately (password at least 12 characters), then run:

```sh
dotnet run --project src/BlueBox.Web -- --create-manager
```

This adds the first manager only; it refuses when an active manager already exists or the requested username is taken. It does not change existing passwords, stock, or history. Remove the Bootstrap values afterward. Existing employee password hashes must be compatible with ASP.NET Core `PasswordHasher<Employee>`; placeholder/plaintext values cannot sign in.

Account administration is scheduled for Milestone 2. There is no public registration.

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

1. Sign in as staff; browse Catalog and Inventory.
2. Select a warehouse/item, check in a positive quantity, and compare the preview with the saved balance.
3. Check out available stock and find the attributed entry in Activity.
4. Attempt to check out too much; verify stock/history remain unchanged.
5. Try zero, negative, fractional, and excessive quantities; verify rejection.
6. Try signing in as the inactive employee.
7. In a disposable test database, exercise competing check-outs and confirm no overselling or lost history.

Stock updates use a serializable SQL Server transaction and a conditional balance update. Log insertion and balance update commit together; a failure rolls both back. SQL Server deadlock victims receive a bounded whole-operation retry. Client-side previews are informational; the server rechecks access, record status, and stock.

Milestone 1 shows activity times explicitly in **UTC**. Central-time display from the mockups can be added during Milestone 2 refinement.

## Automated checks

```sh
dotnet test BlueBox.sln --configuration Release
```

By default, tests run the actual Razor Pages application and stock service against a temporary **SQLite relational test database**. This checks HTTP sign-in, antiforgery, inactive sessions, role policy, searches/pages, movement attribution, invalid input, rollback, and competing stock operations. SQLite is a test dependency only; it is not an application database option.

For SQL Server validation, set `BLUEBOX_TEST_SQLSERVER` privately to a connection string for a **disposable test instance with permission to create databases**, then run the same command. The test harness connects to `master`, creates uniquely named `BlueBoxTest_<guid>` databases, applies the supplied `000`/`001` scripts, and drops only those generated databases afterward. **Do not use the teacher's shared server for this mode.** The database named in the input connection string is not used as the test database.

GitHub Actions runs both modes. The SQL Server job creates a runner-local disposable instance and a generated password; it needs no teacher credentials. Its localhost-only certificate exception applies only to that disposable CI instance.

## Remaining verification

Local relational test results do not prove the teacher's server is reachable or SQL Server locking behaves identically. Record the SQL Server CI result and complete the Windows/teacher-database walkthrough before merging the implementation PR. No remote database was modified by preparing this application.

## Troubleshooting

- **503 / temporarily unavailable:** inspect server-side logs for connection/schema errors. The browser intentionally does not receive raw database exceptions.
- **Invalid column name:** confirm `001` was applied to the exact database in the connection string.
- **Unable to sign in:** check active status and a compatible password hash; a SQL row with a placeholder hash is not a usable login.
- **Unsupported SDK:** install the version pinned in `global.json` and update the editor if necessary.
- **No rows:** populate the empty demo database or provide existing fake records; the app does not seed automatically on startup.
