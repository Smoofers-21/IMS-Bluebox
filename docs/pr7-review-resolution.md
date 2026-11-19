# PR #7 Azure review resolution

The owner supplied `main-review-2.md`, a static review of merged PR #7 at `1959a24`, and requested useful fixes on PR #8 alongside the launch documentation. The review is input; the agreed application behavior, instructor's C#/ADO.NET foundation, and five-table business design remain authoritative.

| Finding | Decision and implementation |
| --- | --- |
| 1 — missing portfolio indexes | Add the four suggested nonunique indexes to fresh Azure installation: filtered correction lookup, history by item/location, and inventory by location including quantity. Add a guarded, transactional, rerunnable [index-only upgrade](../database/azure/002_add_portfolio_indexes.sql) for existing portfolio tables. No columns, constraints, records, or teacher scripts change. |
| 2 — forwarded client address and HTTPS | Add optional `Hosting:KnownProxies`, containing exact verified proxy IPs separated by semicolons. Process one forwarding hop before HTTPS redirection and login limiting. Unconfigured hosts keep existing behavior; unknown peers cannot supply trusted forwarded addresses or schemes. Windows IIS integration may already provide the required forwarding, so verify it before configuring another hop. |
| 3 — database resume | Add bounded SqlClient exponential retry around connection opening only: three total attempts for explicit Azure availability/throttling errors. No command or ambiguous write is replayed. Honor cancellation and preserve the last `SqlException` after exhaustion so the existing unavailable response remains 503. Use a 60-second connection timeout in the configuration builder and Azure example. |
| 4 — local seeder firewall | The initial PR #8 guide already mentions the local computer's firewall rule. Add a reminder next to the seed command; allow that computer's public IP, then remove temporary access when finished. |
| 5 — SQL Server versus Azure evidence | State explicitly that CI uses disposable SQL Server 2022. Query editor results and the live application walkthrough establish Azure behavior separately. No tests target the live portfolio or teacher database. |
| 6 — wording and logs | Explain the actual `YOUR_…` placeholders, give specific optional proxy configuration, and document temporary filesystem application logs for deployment diagnosis without enabling paid monitoring. |

## Changes to the suggested implementation

Do **not** add `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. The [.NET 10 implementation](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/DefaultBuilder/src/ForwardedHeadersOptionsSetup.cs) clears both known-proxy and known-network restrictions when that switch is enabled. Presence of `WEBSITE_SITE_NAME` also does not authenticate a forwarding peer. Exact trusted addresses avoid weakening the earlier spoofing protection. Real App Service/IIS client-address behavior is still a live deployment check.

Do not treat login failure `18456`, wrong-database error `4060`, or every timeout as a transient resume. The opening policy handles `40613`, `40197`, `40501`, `49918`, `49919`, `49920`, and `4221`; other failures surface immediately. A caller-supplied connection string retains its own timeout, so an existing 30-second setting must be edited privately to use 60 seconds. Retries can still exhaust; they do not guarantee free-tier availability.

The index upgrade skips existing same-named indexes rather than replacing them. It is intended for the supplied schema or the matching fresh installer; manually modified index definitions need inspection before use. The filtered index is nonunique: one-child correction authorization and concurrency rules remain enforced by the application transaction.

## Verification

New local regressions exercise trusted HTTPS/client forwarding, independent client login buckets, spoof rejection, invalid proxy configuration, successful connection-open retry, immediate permanent-error failure, retry exhaustion, and cancellation. Provider errors are injected without a network/database connection. SQL regressions inspect all four index keys/filter/include, rerun the upgrade without losing data, reject a wrong target, and exercise competing corrections and a correction chain on the indexed schema.

See the [validation record](validation.md) for executed results tied to the current revision. Azure connection, schema/index deployment, proxy settings, idle resume, live OMDb, and the public website walkthrough remain owner deployment checks. Other previously deferred review suggestions remain outside this change.
