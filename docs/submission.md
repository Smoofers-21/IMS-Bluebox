# Class submission and `.slnx`

The checked-in solution is `BlueBox.sln`; the repository's build instructions and GitHub Actions use it. If the instructor requests `.slnx`, generate that format from the merged solution using the pinned .NET 10 SDK. Changing the extension alone is not a conversion.

## 1. Pull the code being submitted

PR #5 is already merged. After the owner merges any follow-up PR intended for submission, run from the repository folder:

```sh
git switch main
git pull origin main
```

Resolve or preserve any local work before switching branches. Confirm that the merged `main` contains the Milestone 2 screens and the latest documentation; an unmerged PR is not included by pulling `main`.

Follow [development setup](development.md) on Windows: install a Visual Studio release supporting .NET 10 with ASP.NET/web development, configure the teacher's database connection privately, and configure OMDb if used. SSMS administers SQL Server; the app must be able to connect from the machine running it.

## 2. Convert and verify the solution

In the folder containing `BlueBox.sln`, run:

```sh
dotnet sln BlueBox.sln migrate
dotnet restore BlueBox.slnx --locked-mode
dotnet build BlueBox.slnx --no-restore --configuration Release
dotnet test BlueBox.slnx --no-restore --configuration Release
```

The conversion creates `BlueBox.slnx` alongside the existing solution, referencing the same web/test projects. Open `BlueBox.slnx` in Visual Studio and select **BlueBox.Web** as the startup project. Start the website and complete the [demo walkthrough](demo-guide.md) against the intended teacher database.

Default tests intentionally skip the SQL Server integration groups without `BLUEBOX_TEST_SQLSERVER`. Those groups belong on a disposable SQL Server instance or GitHub's runner, never the teacher's shared server. The Windows app walkthrough is separate from that automated suite.

This is a local submission conversion. Keep `BlueBox.sln` in the repository because CI uses it. Adding/changing the checked-in solution format later should use a branch and PR. If a generated `.slnx` already exists, use it or back it up before repeating migration; the command refuses to overwrite an existing file.

When both solution formats are present, use the explicit filename in build/test commands. For the submission copy, keep the `.slnx` and any other files the instructor requests.

## 3. Package the project

Follow the instructor's required upload format. If a ZIP is expected, include the project folder with the following structure and relative paths intact:

```text
BlueBox.slnx
global.json
Directory.Build.props
README.md
src/BlueBox.Web/                 # C# project, Razor views, assets, settings, lock file
tests/BlueBox.Web.Tests/         # Referenced by the solution
database/                       # Includes schema embedded by the test project
docs/                           # Plan, setup, requirements, validation, and demo guide
```

Include tracked project/configuration files such as `.csproj`, `packages.lock.json`, `appsettings.json` with public defaults, and `Properties/launchSettings.json`. The `.slnx` alone contains project references, not the C# code or website assets. The database schema is required to build the test project because its current-schema reference is an embedded resource.

Exclude `.git`, `.vs`, `bin`, `obj`, `TestResults`, local/private settings files, database files/backups, credentials, working passwords, and API keys. User Secrets stay on the local computer; configure access separately on the reviewer's machine using [development setup](development.md). GitHub source code does not itself host a running website or SQL Server.

Before uploading, extract the ZIP to a separate folder and check that `dotnet restore BlueBox.slnx --locked-mode` and `dotnet build BlueBox.slnx --no-restore --configuration Release` succeed there. Re-enter private configuration only where needed for the manual app walkthrough. Retain the original repository checkout.
