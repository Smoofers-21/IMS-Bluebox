# PR #9 deployment review resolution

The owner supplied `pr9-review.md`, a static review of deployment branch commit `ba89a24`, and asked for useful changes on the same PR. The review is input; deployment remains manual, free-tier hosting and existing app behavior are retained, and no database scripts run during deployment.

| Finding | Decision |
| --- | --- |
| 1 — branch enforcement and secret scope | Update setup to require an `azure-portfolio` GitHub environment with an exact main **Branch** deployment rule and the publish profile stored as an environment secret. Remove the repository secret copy. Keep the app-name repository variable and early main/CI gate. These GitHub settings must be configured by the owner; this workspace cannot apply or verify them. |
| 2 — OpenID Connect | Defer the authentication migration for this first demo deployment. The owner has already configured the publish-profile method. OIDC would also require a permitted Azure identity, federated credential, and app-scoped deployment role; those are not established here. The current method uses the protected environment secret and still needs SCM publishing authentication. No additional reviewer approval or repeated SCM-toggle procedure is introduced. |
| 3 — deploy action pin | Pin `azure/webapps-deploy` to commit `02a81bead70021f5284939794bcec79c271ab383` (v3.0.8). Verified the official repository's dereferenced v3 tag and release tag resolve to that commit, and fetched its action metadata. An annotated tag object's SHA is not a deployable commit pin. Future action upgrades remain deliberate reviewed changes; automated update PRs are deferred. |
| 4 — post-deployment check | Add a bounded HTTPS GET check for `/Account/Login`, requiring HTTP 200 and BlueBox's sign-in heading/username/password fields. This rejects a generic Azure page and startup errors, retries for F1 startup, and does not print page bodies or cookies. A failed check fails the job after upload; it does not roll back automatically. |
| 5 — hostname/artifact exposure | Keep the public hostname: the owner explicitly requested a public portfolio link. No extra URL variable is necessary. The ZIP retains its one-day lifetime and contains only published app files, not private settings, database credentials, or starter-login files. |
| 6 — small changes | Add a comment explaining the newest exact-SHA main push CI gate. Ignore `Properties/PublishProfiles/`; existing `*.user` already covers `*.pubxml.user`. Tell the owner to delete the downloaded credential after saving the environment secret and document reset/replacement if exposure occurs. |

## Scope of the smoke check

The review says a successful anonymous login-page response proves database connectivity. That is incorrect for this application: `LoginModel.OnGet` renders the page without reading SQL. The new check proves startup and expected page rendering; a manual account sign-in and the inventory walkthrough still verify the real database. No test credentials are added to Actions, and the check neither authenticates nor creates data.

The main-only shell check alone cannot protect a repository-wide secret from a modified workflow on another branch. Environment branch restrictions and removal of the repository secret copy are essential parts of this setup, not changes automatically made by YAML. No Azure/GitHub configuration or secrets were changed from this workspace.

## Verification

Review source commit `3329215` passed [run 44865499072](https://github.com/Smoofers-21/IMS-Bluebox/actions/runs/44865499072): both jobs succeeded with **105 tests passed, 0 failures** in the SQL-enabled suite (unit tests plus expanded SQL cases). Local Release tests passed **48 tests**, with **38 SQL groups intentionally skipped**; the post-upload script passed eight controlled cases. See the [validation record](validation.md) for scope. The real Azure upload, environment settings, authenticated database workflow, and final launch remain pending owner execution.
