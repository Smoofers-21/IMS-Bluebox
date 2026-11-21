# Demo and portfolio guide

**Status:** portfolio walkthrough for the implemented inventory and manager workflows. Deployment and live verification of the [Azure website](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net) remain pending. Follow [Azure setup](azure-portfolio-setup.md), check the [validation record](validation.md), and use verified fictional data. The owner has configured a public staff account; the login-page update must be deployed and both PublicDemo display settings configured before its demo panel is visible.

## Public visitor access

The [README demo section](../README.md#try-the-demo) contains the current intentionally public `avery.chen` login. On the sign-in page, select **Try the demo**, copy each credential into the normal login form, then select **Sign in**. The fields remain selectable when clipboard access is blocked or JavaScript is disabled. There is no automatic authentication or new permission bypass.

Visitors can browse stock/history and record stock movements. Manager administration and corrections stay restricted. Explain that this is a shared interactive dataset: one visitor's stock changes and free-text notes remain visible to others. Review Activity before presenting; use fictional content. Repeated submissions or refreshes can return 429; wait a minute before retrying. Keep the public account ACTIVE and STAFF, and keep its actual password synchronized with the displayed value and README. Do not promote that account to manager while its credentials are public. Set `PublicDemo__Username` and `PublicDemo__Password` in Azure as described in [display settings](azure-portfolio-setup.md#public-demo-display-settings). To rotate the password, change the actual account password, update the Azure display setting, then update the README through a PR. Settings-only changes do not require a code redeployment. Removing a display setting hides the panel but does not revoke account access.

## Fake data plan

For the current Azure portfolio, the private starter SQL creates one active manager, four active staff, and Fayetteville/Bentonville warehouses. It deliberately creates no items, inventory balances, or transactions. Use the separate private login file; each initial account has its own password. After [deployment through Actions](github-actions-deployment.md), the owner will add catalog entries and opening check-ins through the application before presenting this walkthrough.

For disposable test demonstrations only, `--seed-demo` creates North Loop/West End warehouses, one active manager, one active staff member, one inactive staff member, six catalog entries, balances, and opening history. Do not run it on the account-only portfolio database. See [local initial data setup](development.md#3-choose-initial-data).

Prepare balances that cover zero stock, a small quantity, and enough stock for ordinary movements. Establish opening quantities through recorded check-ins so inventory and movement history reconcile. Only the explicitly public staff demo credentials belong in visitor instructions; manager, SQL, deployment, and API credentials remain private.

## Suggested walkthrough

| Step | Demonstration | Expected evidence |
| --- | --- | --- |
| 1 | Sign in as staff and open the overview | Visible stock totals and recent activity |
| 2 | Search the catalog and open item details | Format, optional poster/rating, and warehouse balances |
| 3 | Check in 10 units against a balance of 5 | Preview and saved balance of 15; attributed log entry |
| 4 | Check out 4 units | Saved balance of 11 and a second log entry |
| 5 | Attempt to check out 12 units from that balance | Clear rejection; balance and history unchanged |
| 6 | Demonstrate a manager-only action with staff access | Server denies the operation |
| 7 | Sign in as manager and add a movie | Select an OMDb match and review returned metadata; item starts with zero stock |
| 8 | Add a game or demonstrate unavailable movie metadata | Optional manual fields do not block saving |
| 9 | Correct the mistaken check-out of 4 | Linked check-in of 4, required explanation, balance restored to 15 |
| 10 | Reverse that correction from its own activity entry | One new linked check-out of 4; both earlier entries remain unchanged |
| 11 | Attempt to archive the stocked item | Rejected because stock remains |
| 12 | Archive a separate zero-stock item | Historical information stays accessible |

The numeric examples assume no intervening movements. Use a fresh demo dataset or document actual starting balances before presenting.

## Checks outside the presentation

- Two competing check-outs cannot oversell stock or lose history.
- A failed movement cannot leave only a balance change or only a history entry.
- Inactive/terminated employees cannot retain authorized access.
- Inactive warehouses and archived items reject movements.
- Corrections require manager access, an original reference, and an explanation.
- No API key or database credential appears in source control or browser responses.

## Portfolio presentation

For the presentation, explain the business problem, walk through one normal movement and one rejected movement, then show the relational model and linked correction history. Emphasize decisions and verified behavior rather than the number of screens.

Suggested screenshot set after validation:

- Overview dashboard using fake data.
- Inventory by warehouse.
- Movement form with resulting balance preview.
- Activity history with a linked correction.
- Item details with movie metadata.

The [validation record](validation.md) contains verified test results. The website link is listed with deployment pending; update that label and add screenshots after manual verification. Share the designated staff demo account and keep manager passwords private. The Azure portfolio uses fictional data. The public staff password is intentionally different from the private manager password.

When demonstrating editing, show that an item’s type/format become fixed after its first movement, that resetting a staff password ends its old session, and that saving the signed-in manager’s own account gives a clear sign-out notice.
