# PR #13 review resolution

The owner supplied `pr13-review.md`, a static review of `3bc2384`, and requested useful fixes on the same PR. Preserve the positive-stock Inventory view, stored poster thumbnails, title/format/item-ID search, existing five-table schema, and free-tier deployment.

| Finding | Resolution |
| --- | --- |
| 1 — numeric titles selecting unrelated IDs | Preserve both bare and # item-ID lookup, but typing now clears the committed selection without choosing a new one or fetching its balance. Commit only on result click, Enter, or an unambiguous complete value on blur. A bare number that appears in an active item's title/format cannot resolve as an ID automatically: list the matching choices instead. # explicitly identifies an ID. This prevents a movie such as 300 or 1917 from silently selecting an unrelated item with that ID. Full result labels retain the format and unique ID. |
| 2 — empty Inventory | With no filters, display No stock on hand. With active search/type/warehouse filters, retain No matching inventory and search/reset guidance. Both cases explain that only stocked items are listed and link to Move stock/check-in and Catalog, so an unstocked item can be found and received. |
| 3 — query shape | Use stored CURRENT_INVENTORY rows joined directly to ITEM and LOCATION for positive-stock lists. Keep the separate item/warehouse cross join only when catalog details explicitly requests zero balances. Both paths keep typed parameters, stable ordering, and SQL pagination. An optimizer might transform the previous query; no claim is made that it always materialized every pair, and no database performance benchmark is claimed here. The direct shape makes the stock-only intent explicit. |
| 4 — external images | Retain the agreed manual HTTPS poster policy for both media types; do not introduce a host allowlist or image proxy. Add width=28 and height=38 to match the existing reserved thumbnail size. Keep lazy loading, async decoding, no-referrer, HTTPS validation, icon fallback, and the strict CSP. |
| 5 — accessibility and comments | Debounce matching-count announcements by 300 ms, cancel pending counts on close/selection, announce selected results immediately, and avoid assigning identical status text repeatedly. Add a comment that the hidden select remains the submitted item ID. Keep the existing narrow thumbnail error handler. |

## Verification

The reviewed source `3bc2384` passed [run 44943383445](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44943383445): both jobs succeeded, and its SQL-enabled suite passed 127 tests. That result applies to the reviewed source, rather than these later fixes.

The review changes passed locked restore and local Release checks: **63 passed, 44 SQL groups skipped, 0 failures**. Existing stock-visibility, filter/paging, details, and thumbnail cases cover both SQL shapes; a new SQL regression checks the empty-inventory heading, check-in/Catalog links, and active-filter guidance. Thumbnail rendering tests also verify explicit dimensions. The local SQL helper was attempted, but the Microsoft image download returned 403 before SQL Server started. No local SQL pass or live Azure database access is claimed; verify the latest PR's SQL-enabled CI before merging.

Chromium component checks passed numeric title/ID collisions for 300 and 1917, explicit result/Enter/blur commits, no per-keystroke balance requests, debounced announcements and stale-count cancellation on selection, submitted-ID safety, keyboard/pointer/format search, bounded matching results, preview rules, preselection, no-JavaScript fallback, thumbnail success/failure/no-referrer, strict CSP, and 320/375/768/1440-pixel layouts. These use controlled component fixtures and balance responses, not a live SQL connection. JavaScript syntax, documentation links, the runtime SQL catalog, and the Markdown/Word plan were checked.

No DDL, database data, API keys, roles, or hosting settings are changed. Merge after current CI succeeds, then run the existing manual Azure deployment workflow to apply the reviewed behavior.
