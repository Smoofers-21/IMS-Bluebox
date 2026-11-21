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

PR #5 merged the application, documentation, and schema-compatible review fixes into main at `13b4f43`. Follow-up improvements are documented in the [main-review resolution](main-review-resolution.md). Check the [validation record](validation.md) and current revision’s CI; teacher-server verification remains separate.

- [x] Manager tools for items, employee accounts, and locations.
- [x] Last-active-manager protection when managing accounts.
- [x] Manager corrections linked to originals, with a settled repeated-correction policy.
- [x] Zero-stock item/location retirement rules.
- [x] Server-side OMDb movie lookup, manual fallback, optional posters and age ratings.
- [x] Overview dashboard and responsive layouts following the existing visual style.
- [x] Verify Milestone 2 unit/routing and real SQL Server tests, including competing manager edits, retirement, corrections, password reset protection, and dashboard consistency; publish reports in GitHub Actions.
- [ ] Finish Windows/class walkthrough, live OMDb smoke check, and final fake-data screenshots.
- [x] Update setup, requirements, correction policy, and demo/review documentation.

**Complete when:** agreed features pass automated checks and the Windows/teacher-server walkthrough, live OMDb check, and final demonstration materials are complete. Source and CI are ready for owner review; final class sign-off is still pending.

## Class hand-in

After the owner merges any follow-up PR being submitted, pull `main`, verify the Windows setup, and use the [submission guide](submission.md) to generate `.slnx` when required. Keep project folders and required build files alongside the solution. Submit according to the instructor’s requested format.

## Current phase — Free Azure portfolio deployment

The owner is transitioning to a web-only portfolio. Follow the [Azure setup guide](azure-portfolio-setup.md); the application workflows and five-table design stay the same.

- [x] Prepare a guarded, transactional Azure-compatible fresh-schema installer and setup instructions.
- [x] Record the owner-reported Azure SQL creation, schema-query execution, and App Service hostname.
- [x] Add the [website address](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net) with an explicit launch-pending label.
- [x] Implement portfolio index scripts, bounded connection-open retry, and optional exact trusted-proxy configuration; see the [Azure review resolution](pr7-review-resolution.md).
- [x] Prepare a [manual GitHub Actions deployment workflow](github-actions-deployment.md), limited to main with successful CI; no local SDK installation required for deployment.
- [ ] Owner verifies the SQL free offer, monthly-limit auto-pause, and access in the chosen subscription/region.
- [x] Record the owner's report that the schema/index SQL, app settings, and outbound-IP firewall rules were applied.
- [ ] Confirm account-only starter setup: one manager, four staff, Fayetteville/Bentonville warehouses, and no items/stock/history until entered in the app.
- [x] Implement the [PR #9 review changes](pr9-review-resolution.md): pinned Azure action, bounded post-upload sign-in check, and protected-environment setup instructions.
- [ ] Keep the app-name repository variable, move the profile to an azure-portfolio environment secret, restrict that environment to main, and remove the repository secret copy.
- [ ] Merge the deployment workflow, confirm F1/.NET 10, and run the first manual deployment with its startup check.
- [ ] Verify staff/manager workflows, OMDb, and behavior after free-tier idle shutdown/resume.
- [ ] Mark the website launched, publish verified staff access instructions, and add final fake-data screenshots.

No Azure database or website has been modified by this workspace. The owner reports creating the resources and running the schema installer; the application has not been verified live. Never reuse teacher credentials or other students' data.

## Scope control

No customer rentals, payment processing, kiosk inventory, streaming, public registration, or mandatory first-login password change is planned. New application work goes through a branch and PR; the project owner merges it. Do not push directly to `main` or merge PRs on the owner's behalf.
