# Two-milestone implementation roadmap

The project owner combined the original six stages into two milestones. Use smaller branches/PRs within each milestone; the owner reviews and merges into `main`.

## Milestone 1 — Working inventory system

- [x] C# / ASP.NET Core Razor Pages solution and pinned .NET SDK.
- [x] SQL Server model mappings and a reviewable schema-update script.
- [x] Private connection configuration, demo seeder, and initial-manager setup.
- [x] Employee login/logout, current-status validation, and Staff/Manager role policy.
- [x] Searchable read-only catalog, item details, and warehouse inventory.
- [x] Check-in/check-out with balance previews and server-side validation.
- [x] Atomic stock/history writes and permanent activity browsing.
- [x] Instructor-template ADO.NET data access, unit/routing checks, and a real SQL Server integration path.
- [x] Verify the Milestone 1 GitHub Actions SQL Server run ([run 44132304808](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44132304808), commit `02e60c5`).
- [ ] Owner applies/reviews DDL and tests on Windows against the teacher's database.

**Complete when:** an employee can sign in, find warehouse stock, record movements, and review accurate history on the intended SQL Server setup. Invalid movements leave inventory unchanged. Unit/routing results alone do not close this milestone.

## Milestone 2 — Complete demo and portfolio project

Implementation is on `feature/milestone-2`. Checked features describe code delivered for review; SQL Server CI and the teacher-server walkthrough still gate completion.

- [x] Manager tools for items, employee accounts, and locations.
- [x] Last-active-manager protection when managing accounts.
- [x] Manager corrections linked to originals, with a settled repeated-correction policy.
- [x] Zero-stock item/location retirement rules.
- [x] Server-side OMDb movie lookup, manual fallback, optional posters and age ratings.
- [x] Overview dashboard and responsive layouts following the existing visual style.
- [ ] Verify Milestone 2 SQL Server CI, finish Windows/class walkthrough, and capture final screenshots.
- [x] Update setup, requirements, correction policy, and demo/review documentation.

**Complete when:** agreed features work together and the system is ready for class review and GitHub presentation.

## After grading — Public deployment

Host the website and a separate protected database containing fake demo data. Configure the new connection string outside source control, then add the verified live link to the README. Never reuse teacher credentials or move other students' data.

## Scope control

No customer rentals, payment processing, kiosk inventory, streaming, public registration, or mandatory first-login password change is planned. New application work goes through a branch and PR; the project owner merges it. Do not push directly to `main` or merge PRs on the owner's behalf.
