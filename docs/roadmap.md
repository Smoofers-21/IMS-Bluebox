# Implementation roadmap

The planning baseline is documented. All implementation milestones below are pending; checkboxes represent unfinished work, not delivered features.

## 1. Database and development foundation

- [ ] Inspect the actual SQL Server schema and confirm supported versions and connection requirements.
- [ ] Select the .NET version, data access approach, and authentication facilities.
- [ ] Prepare targeted schema updates for the agreed metadata, statuses, correction link, and constraints.
- [ ] Create fake demonstration data with traceable opening stock movements.
- [ ] Document repeatable local setup and secret configuration without committing values.

**Complete when:** the application can connect to the intended database, schema updates are verified, and demo balances reconcile to their movement history.

## 2. Sign-in and navigation

- [ ] Build the shared layout following the supplied mockups.
- [ ] Implement employee sign-in/sign-out, status checks, and Staff/Manager authorization.
- [ ] Provide initial manager setup and simple manager-controlled employee accounts.
- [ ] Protect the last active manager from deactivation/demotion.

**Complete when:** staff and manager access are verified, inactive accounts are blocked, and protected operations reject unauthorized requests.

## 3. Catalog and warehouse administration

- [ ] Build searchable catalog, item details, and manager item editing.
- [ ] Add optional poster and age-rating fields with manual entry.
- [ ] Build warehouse management and stock-based retirement rules.
- [ ] Ensure new items do not create stock outside the movement workflow.

**Complete when:** managers can maintain catalog and locations, staff can browse them, and retirement preserves history and obeys zero-stock rules.

## 4. Stock movements and history

- [ ] Build inventory by warehouse and check-in/check-out with balance previews.
- [ ] Save balances and log entries atomically.
- [ ] Handle insufficient stock, invalid quantities, and competing updates.
- [ ] Build activity filtering and manager corrections linked to originals.
- [ ] Settle and validate repeated-correction safeguards.

**Complete when:** valid movements reconcile to balances, rejected/failed movements leave no partial changes, concurrent check-outs remain consistent, and correction permissions/history are verified.

## 5. Movie lookup and dashboard

- [ ] Add server-side OMDb lookup and manager match selection.
- [ ] Populate optional posters/age classifications and retain manual fallback.
- [ ] Build stock summaries and recent activity on the overview screen.

**Complete when:** lookup and failure paths work without exposing the key, and overview totals agree with inventory.

## 6. Demo and portfolio preparation

- [ ] Run the agreed acceptance scenarios and record actual outcomes.
- [ ] Capture screenshots from the working application using fake data.
- [ ] Add verified run instructions and screenshots to the README.
- [ ] Prepare a short demonstration using the demo guide.
- [ ] Update documentation to distinguish delivered features from remaining limitations.

**Complete when:** the documented setup reproduces the app, the core workflow has evidence, and portfolio claims reflect the implementation.

## Scope control

No customer rentals, payment processing, kiosk tracking, streaming, public registration, or mandatory first-login password change is planned. Revisit scope only through an explicit project decision. No delivery dates or completed implementation milestones are claimed yet.
