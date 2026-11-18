# Demo and portfolio guide

**Status:** walkthrough for the implemented Milestone 1 and 2 features. Check the [current validation record](validation.md#verified-results) and the current revision’s CI checks, then run the presentation on verified fake data in the intended Windows setup. The teacher-server walkthrough, live OMDb smoke check, and final screenshots remain review tasks; this guide is not proof they have been completed.

## Fake data plan

The explicit `--seed-demo` command creates two fictional warehouses (North Loop and West End), one active manager (jordan.lee), one active staff member (avery.chen), one inactive staff member (sam.rivera), and six catalog entries. Moonlight Signal has separate Blu-ray/DVD entries; three games have separate platform formats. Set the shared initial demo password privately. See [initial data setup](development.md#3-choose-initial-data).

Prepare balances that cover zero stock, a small quantity, and enough stock for ordinary movements. Establish opening quantities through recorded check-ins so inventory and movement history reconcile. Do not publish working passwords or connection credentials in the guide.

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

The [validation record](validation.md) contains verified test results. Add screenshots and a live demo link after the corresponding manual verification/deployment. This is a fictional class/portfolio project, not a deployed business system.

For class hand-in, follow the [submission guide](submission.md) and include the requested `.slnx` with its project files. Public hosting follows grading and uses a separate fake-data database.

When demonstrating editing, show that an item’s type/format become fixed after its first movement, that resetting a staff password ends its old session, and that saving the signed-in manager’s own account gives a clear sign-out notice.
