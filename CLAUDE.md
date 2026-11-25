# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# TakeawayOS
An online ordering and order management system for an independent takeaway restaurant. Built as a portfolio project by a pre-university student. The system will be deployed and used by a real business — my parents' takeaway — replacing telephone-only ordering with a live web platform.

---
## Tech Stack
| Layer | Choice |
|---|---|
| Backend | ASP.NET Core Web API, .NET 10, C# |
| ORM | Entity Framework Core 10 (Npgsql provider) |
| Database | PostgreSQL |
| Auth | ASP.NET Core Identity + JWT |
| Frontend | React (JavaScript), Vite |
| Version Control | Git + GitHub |
| Hosting | Render (API + Postgres) + Vercel (frontend) |
---
## Project Structure
```
/TakeawayOS
  /TakeawayOS.API          → ASP.NET Core Web API project
    /Controllers           → HTTP endpoints
    /Models                → EF Core entity classes
    /DTOs                  → Request/response shapes (never expose raw entities)
    /Data                  → DbContext, migrations
    /Services              → Business logic, kept out of controllers
  /TakeawayOS.Frontend     → React + Vite frontend
    /src
      /components          → Reusable UI components
      /pages               → Route-level page components
      /api                 → Fetch wrapper functions for calling the backend
```
---
## Commands
**Backend (run from `/TakeawayOS.API`)**
```
dotnet run                          # start API (dev)
dotnet build                        # build
dotnet ef migrations add <Name>     # add EF Core migration
dotnet ef database update           # apply migrations to DB
dotnet test                         # run tests (needs no secrets: TestWebAppFactory supplies its own via UseSetting)
```
**Frontend (run from `/TakeawayOS.Frontend`)**
```
npm install       # install dependencies
npm run dev       # start Vite dev server
npm run build     # production build
npm run lint      # lint
```

**Testing Stripe payments locally (no frontend needed).** Swagger alone can't do it: the card-confirmation step is a Stripe API call (standing in for the browser), and the webhook is Stripe calling us — so it's a Swagger → Stripe CLI → Swagger flow. Needs `STRIPE_SECRET_KEY` + `STRIPE_WEBHOOK_SECRET` in user-secrets, and the Stripe CLI logged in (`stripe login`).
1. **Terminal A** — API: `dotnet run` from `/Takeaway-OS.API`. The `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development` and serves on **http://localhost:5092** (the port `VITE_API_URL` points at).
2. **Terminal B** — forward webhooks: `stripe listen --forward-to localhost:5092/api/webhooks/stripe`. If it reports an expired key, run `stripe login` again (CLI logins last 90 days). It prints the `whsec_...` for `STRIPE_WEBHOOK_SECRET` (set it once, restart the API). **Watch this window** — it shows `<-- [200]` when the webhook accepts an event (a `[400]` = signature rejected).
3. **Create an order** — Swagger `POST /api/orders` (shop must be open, or it's a 409 — add a temporary `POST /api/openinghours` window and delete it after). The response's `clientSecret` is `pi_XXX_secret_YYY`; the **PaymentIntent id** is the `pi_XXX` before `_secret_`.
4. **Confirm the payment** (Stripe CLI — the step Swagger can't do): `stripe payment_intents confirm pi_XXX --payment-method pm_card_visa --return-url https://example.com` (`pm_card_visa` = always-succeeds test card; `--return-url` is required while redirect-based methods are enabled). Look for `"status": "succeeded"`.
5. **Verify** — the webhook flips the order to `Paid`; check via Swagger `GET /api/orders/{id}` (needs Owner auth: log in, paste the token into Swagger's **Authorize**).
6. **Idempotency** — `stripe events resend evt_XXX` (id from Terminal B) → the order stays `Paid`, unchanged.
---
## Architecture Decisions — Know These
These are deliberate choices, not defaults. If asked to change them, flag it first.

**Price snapshotting on orders**: When an order is created, copy `item name` and `unit price` into the `OrderItems` table at that moment. Do NOT store only a foreign key to `MenuItems`. This preserves historical order accuracy if menu prices change later.

**DTOs over raw entities**: Controllers never return EF Core entity objects directly. Always map to a DTO first. This prevents accidental data leaks and keeps the API contract stable.

**Services layer**: Business logic lives in `/Services`, not in controllers. Controllers handle HTTP concerns only (routing, request parsing, response codes).

**Order status is a fixed set of values**: `OrderStatus` enum — `Pending` (placed, awaiting payment), `Paid` (Stripe confirmed), `Preparing`, `Ready`, `OutForDelivery` (delivery orders only), `Completed`, `Cancelled`. Valid forward flow: `Pending → Paid → Preparing → Ready → (OutForDelivery →) Completed`. `Cancelled` is only reachable from `Pending` or `Paid` — once an order is `Preparing`, the kitchen has already started, so cancelling past that point needs a staff override rather than a plain cancel action.

**Payment confirmation comes from Stripe's webhook, not the frontend**: Never mark an order `Paid` because the browser says the payment succeeded — that request can be faked or lost on a bad connection. Stripe calls a dedicated endpoint on the API directly (e.g. `POST /api/webhooks/stripe`) when a payment actually completes. That handler must (1) verify the request came from Stripe using `STRIPE_WEBHOOK_SECRET` to check the signature header, and (2) be idempotent — Stripe can send the same event more than once, so check whether the order is already `Paid` (or track the Stripe event ID) before processing it again.

As built, the specifics:
- **The `PaymentIntent` is created during order creation.** `POST /api/orders` saves the order (`Pending`), then `OrderService.CreateAsync` creates a Stripe PaymentIntent for the amount from `OrderService.ComputeTotal(order)` — the **server-computed** total (`sum((UnitPrice + modifier deltas) × Quantity) + DeliveryFee`), **never** a total sent by the client. The response is `OrderCreateResponseDto { order, clientSecret }`: the browser uses `clientSecret` to complete payment.
- **`Order.Total` is a snapshot, like `UnitPrice` and `DeliveryFee`.** `ComputeTotal` runs exactly once per order, in `CreateAsync`; its result is stored on `Orders.Total`, sent to Stripe as the PaymentIntent amount, and read back by `MapToDto`/`MapToGuestDto` — never recomputed on read. So the charge, the Owner's list and the guest receipt are one number, and a later change to the pricing formula cannot rewrite what an old order cost. The `AddOrderTotalAndPaidAt` migration is **hand-edited**: it backfills existing rows with the per-line formula they were actually charged under. `seed-demo-orders.sql` computes `Total` the same way, since hand-inserted rows skip `CreateAsync`; keep it in step with `ComputeTotal`.
- **`Order.PaidAt`** is set by `MarkOrderPaidAsync` to **Stripe's** time for the payment (`stripeEvent.Created`, UTC), not our clock when the webhook lands — the two differ when the webhook had to wake a sleeping server or was retried. The owner board's "waiting N min" counts from it, falling back to `CreatedAt` for rows that predate the column (left `NULL` by the migration rather than invented).
- **`Order.StripePaymentIntentId` is the order↔payment link and the idempotency key.** Set when the order is created (nullable — pre-Stripe orders have none). The webhook finds the order by this id and no-ops if it's already `Paid`, so Stripe's at-least-once retries are safe. Amounts are sent to Stripe in **pence** (the smallest currency unit, a whole number — £8.50 → `850`); currency is fixed to `gbp`.
- **`OrderService.MarkOrderPaidAsync(paymentIntentId, paidAtUtc)` is the ONLY path to `Paid`.** It lives on `OrderService` with the other status transitions but deliberately bypasses `IsValidTransition` (which rejects `Paid` for every other caller). It only promotes `Pending → Paid`; a `Cancelled` order is **not** resurrected (`NotPayable`). A payment Stripe recorded more than `PendingOrderExpiryService.LatePaymentThreshold` (30 + 5 min) after the order was created is **refunded and the order cancelled** (`LatePaymentRefunded`) instead: it can only have got through because the server was asleep and the expiry job never voided it. Refund first, then save; a failed save is a 500, Stripe retries, and the idempotent refund makes the retry safe. Outcomes: `MarkedPaid` / `AlreadyPaid` (duplicate delivery) / `OrderNotFound` / `NotPayable` / `LatePaymentRefunded` — the webhook returns **200 for all of them** (the event was received and handled) but logs the odd ones, since a payment with no matching order, or against a cancelled order, signals a mismatch a human should look at (possible manual refund).
- **`StripeService` (`IStripeService`) is the sole boundary to the Stripe SDK** — nothing else calls Stripe directly, mirroring the services-layer rule. `CreatePaymentIntentAsync` creates the PI (and tags it with `metadata.orderId` for dashboard traceability — not used for our idempotency, which is the stored id). `ConstructEvent` verifies the signature and returns the parsed `Event`, or `null` on failure. It passes `throwOnApiVersionMismatch: false` so a genuine webhook isn't rejected just because Stripe's account API version differs from the SDK's compiled version — the **signature** is what authenticates it.
- **The webhook reads the RAW request body** (`StreamReader` on `Request.Body`, no model binding) because signature verification must run over the exact bytes Stripe sent — deserializing first would change them and break the check. It rejects a **missing/empty** signature header with `400` up front (a genuine Stripe call always carries one; passing null to the verifier throws differently than a signature mismatch), and an **invalid** signature with `400` via `ConstructEvent` returning null. Only `payment_intent.succeeded` is acted on; every other event type is acknowledged with `200` and ignored (so Stripe stops resending). The endpoint is `[AllowAnonymous]` — Stripe isn't a logged-in user, the signature is the auth.
- **`StripeConfiguration.ApiKey` is set once at startup in `Program.cs`** from `STRIPE_SECRET_KEY`. If it's absent the app still boots (only order creation fails, loudly, with "No API key provided") so the rest of the API works in dev without Stripe configured. Library: the official **`Stripe.net`** package.
- **If the Stripe call fails during order creation** (bad key / network), the exception propagates to a 500 and the order is left `Pending` with no PaymentIntent — a benign, expected state (an abandoned checkout looks identical). Not treated as a validation error.
- **Totals under Stripe's GBP minimum (`IStripeService.MinimumCharge`, £0.30) are a 400**, checked before the order is saved — otherwise Stripe's rejection would arrive after the save, as a 500 plus an orphaned `Pending` order.
- **Cancelling reverses the money, in the same action.** `Paid → Cancelled` refunds the full charge (`StripeService.RefundAsync`); `Pending → Cancelled` cancels the PaymentIntent (`CancelPaymentIntentAsync`) so the customer can no longer pay it. If the customer paid in the moment before a Pending cancel (Stripe refuses to cancel a succeeded PaymentIntent), the cancel returns **409** (`PaymentInProgress`) and changes nothing; the webhook then marks it `Paid`, and cancelling again refunds it. Orders with no `StripePaymentIntentId` (seeded demo data) cancel without calling Stripe.
  - **Paid → Cancelled claims first, refunds second** (`OrderService.CancelAndRefundAsync`): save `Cancelled` (concurrency-checked), then refund; if the refund throws, restore `Paid` and rethrow (500). So money only moves once this cancel has definitely won a race with another device, and a failed refund leaves the order back on the board for a retry. If restoring also fails, the exception says the order is Cancelled but **not** refunded and must be fixed by hand. Pending → Cancelled voids **before** saving instead, because voiding is harmless if the save then loses a race.
  - **Refund idempotency key is `refund-{paymentIntentId}`**, not the order id: order ids repeat across databases (dev and demo share one Stripe test account, and so would every shop), and Stripe rejects a reused key with different parameters.
  - **`resource_missing`** (the PaymentIntent doesn't exist in the current Stripe account — created under an earlier key or account, e.g. test before go-live) is treated as "nothing to void/refund here": cancel proceeds; a skipped refund is logged at **Error** for a human to refund in the other account.
- **`Order.Version` is an optimistic concurrency token mapped to Postgres's `xmin` system column** (`IsRowVersion()` on a `uint`; the `AddOrderConcurrencyToken` migration emits no DDL). Every order save includes the loaded `xmin` in its `WHERE`, so a save from a stale copy throws `DbUpdateConcurrencyException` instead of overwriting another device's change. `OrderService.TrySaveAsync` turns that into `ConcurrentUpdate` → **409** "changed somewhere else, refresh" on the owner status, driver status and driver-assignment endpoints, and detaches the stale entity so later saves on the same context aren't poisoned. The webhook lets the exception become a 500 on purpose: Stripe's retry then sees the current state. The in-memory test provider never changes the token, so concurrency is verified against real Postgres, not in unit tests.
- **Unpaid orders expire after 30 minutes.** `PendingOrderExpiryService` (a `BackgroundService` on a 5-minute `PeriodicTimer`) calls `OrderService.ExpireStalePendingOrdersAsync`, which voids each stale `Pending` order's PaymentIntent through the same rule as the Owner's cancel, then marks it `Cancelled`. Without it a PaymentIntent stays payable forever, so a tab left open at closing time could land a `Paid` order on the board hours later — opening hours are only checked at creation. Each pass **keyset-pages by `Id`** (batches of 50, at most 500 per pass) rather than re-reading "the oldest 50": orders it can't finish (a Stripe failure, or a payment whose webhook never came) stay `Pending`, and the old query picked those same 50 forever, starving newer ones. An order Stripe says was actually paid is left for the webhook. The **first pass runs 30 s after startup** (not a full interval later), because on a host that sleeps when idle every wake-up is a startup; the late-payment backstop in `MarkOrderPaidAsync` covers the payments that arrive before even that. Runs in-process, so it assumes **one instance** (like the startup migrations).

**Menu item "disable" = soft delete**: Disabling a menu item sets `IsAvailable = false` on the `MenuItems` row — the row stays. The public menu query filters `WHERE IsAvailable = true`; the owner's admin view shows everything, active or not, so items can be switched back on without losing their description, price, or image. Disable is the default action for anything that might come back (seasonal, out of stock).
- A separate **Delete** (`DELETE /api/menuitems/{id}`, on the owner menu page) removes the row permanently, for items added by mistake. It is safe for order history because `OrderItems` snapshot `ItemName`/`UnitPrice` and have **no FK** to `MenuItems`; the item's modifier-group links cascade with it. A basket still holding a deleted item fails at checkout with "MenuItem N does not exist", the same as a disabled one.

**CORS is an explicit allow-list, not a wildcard**: The frontend (Vercel) and backend (Render) are different origins, so browsers block requests between them unless the API explicitly allows it. Configure CORS in `Program.cs` with a specific list of allowed origins from config/env (e.g. `http://localhost:5173` in Development, the real Vercel URL in Production) — never `AllowAnyOrigin()` on an API that also handles auth tokens.

As built, the specifics:
- Origins are read from the `Cors:AllowedOrigins` config array into a named policy (`"FrontendPolicy"`). `appsettings.Development.json` holds `http://localhost:5173`; Production sets the Vercel URL via env var (`Cors__AllowedOrigins__0=https://...`). The policy uses `.AllowAnyHeader().AllowAnyMethod()`.
- **No `.AllowCredentials()`** — the JWT rides in an `Authorization` header (set from `localStorage`), which is *not* a CORS "credential" (that means cookies). Adding it would be unnecessary and can't be combined with a wildcard origin anyway.
- `app.UseCors(...)` is placed **before** `UseAuthentication`/`UseAuthorization`, because a preflight `OPTIONS` request carries no JWT and must be answered by CORS before auth would reject it.

**Business hours are enforced server-side, not just shown in the UI**: An `OpeningHours` table holds `DayOfWeek` + `OpenTime` + `CloseTime` (multiple rows per day supported, for a split shift). A manual override flag (e.g. `IsTemporarilyClosed`, with an optional reason) lets the owner shut ordering for a one-off holiday without touching the weekly schedule. The check happens in the `Services` layer at order submission time — the frontend shows the same status as a "we're closed" banner, but the backend is what actually rejects the order. Time comparisons use a configured IANA time zone (`RESTAURANT_TIMEZONE`, defaulting to `Europe/London`), not the server's own clock, since Render's servers run in UTC.

As built, the specifics:
- `IsTemporarilyClosed` + `ClosureReason` live on a **single-row `RestaurantSettings` table** (`Id` always 1, seeded by the `AddBusinessHours` migration via `HasData`). It's a plain flag with **no expiry** — it stays on until the Owner clears it, so forgetting to flip it back means the shop silently takes no orders. An auto-expiring `ClosedUntil` was considered and rejected for v1 simplicity; revisit if that actually bites.
- **A closed shop is a `409 Conflict`, not a `400`.** The basket isn't malformed — there's nothing for the customer to fix — so `OrderCreateResult.RestaurantClosed` distinguishes it from ordinary validation failures, and the frontend uses that to show the "we're closed" banner rather than a field error.
- **A window with `CloseTime <= OpenTime` runs past midnight** (e.g. Fri 17:00 → 00:30). That's how the schedule encodes late closing — no end-date column. `BusinessHoursService` therefore checks *yesterday's* rows as well as today's, so at 00:15 on Saturday the shop is open on **Friday's** row. "Closed" is simply the absence of any matching window; there is no `IsClosed` column.
- `IBusinessHoursService.GetStatusAsync()` has exactly two callers — the public `GET /api/openinghours/status` endpoint (the banner) and `OrderService.CreateAsync` (the actual rejection). Sharing one method is what guarantees the banner can't say "open" while the server refuses the order. Keep it that way.
- Owner-only writes: `POST`/`PUT`/`DELETE /api/openinghours` for the schedule, and `PUT /api/openinghours/closure` for the holiday override (idempotent — reopen by PUTting `IsTemporarilyClosed: false` to the same route, which also clears the reason so a stale "Closed for Christmas" can't resurface). The closure PUT returns the resulting `RestaurantStatusDto`, so the owner immediately sees what the customer sees.
- **Overlapping windows are rejected**, and the check is not day-by-day: a past-midnight window physically occupies two days, so Fri 17:00→00:30 really does clash with a Sat 00:00 window despite the rows naming different days. `BusinessHoursService` flattens every window onto a circular "minutes since Sunday 00:00" weekly timeline and compares intervals there. `OpenTime == CloseTime` is also rejected — ambiguous between a zero-length window and a full 24 hours.
- An invalid window (overlap / zero-length) is a plain **400** — the owner can fix it by picking different times. That's the opposite call to the customer-facing 409 above, and deliberately so: 409 there means "nothing about your request is wrong, we're just shut".
- Real opening hours are the owner's *data*, so they stay out of migrations — only the `RestaurantSettings` singleton is seeded structurally. `Data/seed-opening-hours.sql` exists to bulk-load a dev schedule.

**The delivery fee is Owner-configured and snapshotted onto the order**: `RestaurantSettings.DeliveryFee` holds the *current* fee, editable by the Owner (the same singleton row as the closure flag). `Orders.DeliveryFee` is a **snapshot** taken at creation, on exactly the same reasoning as `OrderItems.UnitPrice`: raising the fee next month must not silently rewrite what last month's orders cost. `ComputeTotal` becomes `sum((UnitPrice + modifier deltas) × Quantity) + Order.DeliveryFee`, so the amount charged in Stripe, the Owner's list, and the guest receipt all pick the fee up from one place and cannot diverge.
- The fee is **per order, not per line**, so it must never go through `lineTotal` in `utils/basket.js`. The frontend adds it once, at the basket/checkout summary level. Putting it inside `lineTotal` would multiply it by the number of lines.
- **Collection orders snapshot `0`**, which is why `ComputeTotal` adds the column unconditionally with no `OrderType` branch. "Does this order pay for delivery?" is decided once, at creation, and is then a recorded fact about the row rather than a rule re-evaluated on every read.
- **Every delivery order pays the fee.** No minimum order value and no free-delivery-over-£X threshold: both were considered and rejected for v1. Either one, if it's ever wanted, is a conditional at snapshot time, not a change to the formula.
- Seeded structurally at `0.00`, not at a realistic price, for the same reason opening hours stay out of migrations: the real number is the owner's *data*. A fresh database therefore charges nothing for delivery until the Owner sets a fee.

**The delivery area is a list of postcode districts, matched on the outward code exactly**: A delivery order to a postcode outside the allowed districts is refused at creation. `DeliveryArea` is its own table (`Id` + `OutwardCode`, unique-indexed), not a column on `RestaurantSettings` — the same call as `OpeningHours`, and for the same reason: it's a variable-length list of owner data, so the unique index makes "no duplicate districts" a database guarantee instead of string-splitting in C#. Owner CRUD lives on `IDeliveryAreaService` / `DeliveryAreasController`, separate from `IRestaurantSettingsService` (which stays a fixed set of typed fields on the singleton row).
- **Matching is on the OUTWARD code, compared for exact equality — never `StartsWith`.** This is the whole reason districts are stored rather than prefixes: under prefix matching the district `E1` would silently also match `E14 5AB`, committing the shop to a different part of London. `UkPostcode.TryParse` splits the postcode first (the inward code is *always* exactly three characters, so the outward code is everything before them, whatever the shape), and only then compares. `PostcodeParsingTests` pins down all six postcode shapes and the E1/E14 case specifically.
- **An empty district list means the shop delivers NOWHERE, not everywhere.** Fail-closed, and it needs no branch to achieve — "is this code in the set" is naturally false for every code when the set is empty. Fail-*open* would have meant writing an explicit "if no districts, allow everything", i.e. promising deliveries the Owner never said they could make. The consequence is real and shares a failure mode with `IsTemporarilyClosed`: a fresh database refuses every delivery order until the Owner adds a district, so the settings card renders a prominent warning whenever the list is empty. Not seeded by the migration — real districts are the Owner's data, same call as opening hours.
- **Outside the area is a 400 via the existing `OrderCreateResult.Error`, NOT the 409 the closed shop uses.** 409 in this codebase means specifically "nothing about your request is wrong, we're just shut". An out-of-area postcode *is* something about the request, and the customer has two things they can do about it — a different address, or switch to collection — so the message says so. No new flag on `OrderCreateResult` and no new frontend branch.
- **The postcode is its own field, not scraped out of `DeliveryAddress` with a regex.** `OrderCreateDto.DeliveryPostcode` + a dedicated checkout input; a saved address supplies it from `Address.Postcode` and the saved copy **wins over anything the request sent** (same reasoning as `CustomerId` coming from the JWT). Guessing which part of a free-text blob is the postcode is not a good enough basis for refusing someone's money.
- **An unparseable postcode is a refusal, not a pass.** Deliberately the opposite call to `CustomerPhone` carrying no `[Phone]` attribute: the phone number isn't used to decide anything, and this is. "Couldn't check" must never silently become "allowed". The regex is otherwise kept lenient (it doesn't enforce the official per-position letter exclusions) because every extra rule is another way to wrongly refuse a real customer.
- `Orders.DeliveryPostcode` is a **snapshot**, normalised to `"E1 6AN"`, on the same reasoning as `DeliveryFee` and `OrderItems.UnitPrice`: narrowing the area next month must not retroactively make a delivered order look invalid. Empty on collection orders and on rows predating the column — both genuinely have none. `DeliveryAddress` stays the complete human-readable string and still *contains* the postcode (`FormatAddress` appends it for saved addresses; `CreateAsync` appends it for free-text ones), so the Owner list, driver dashboard and guest receipt needed no changes. Present on `OrderDto`, deliberately **absent** from `GuestOrderDto` — the guest's own address line already shows it, and that DTO stays as narrow as the confirmation page allows. That absence is a display decision, not a security one: guests are held to the radius check like everyone else, because enforcement is in `CreateAsync`, which has no guest branch.
- **The frontend does not re-implement the postcode grammar.** `utils/postcode.js` mirrors exactly one thing — that the inward code is the last three characters, a fact about the format rather than a rule of ours — and gets the districts themselves from the server. Validity and deliverability both stay server-side, for the reason `lineTotal` exists and `GetStatusAsync` has one caller-shared method. The resulting warning is **advisory and never disables the submit button**, so the server's own 400 stays reachable through the UI (contrast the delivery-fee card, whose client-side `isValid` makes its 400 unreachable in situ).

**Customer accounts are optional, not required**: Guest checkout (name/phone/address/notes, no login) stays fully supported and is the default path — accounts are additive on top of it, not a replacement. A `Customer` role sits alongside the `Owner`/`Driver` roles in Identity — there is no `Staff` role (see V1 Scope). `Orders.CustomerId` is a nullable FK — null for guest orders, populated for logged-in orders. Guest contact fields (name/phone/address) stay on `Orders` regardless of whether a `CustomerId` is present, since delivery/contact needs them either way. A registered customer gets saved addresses, order history, and faster repeat checkout.

**A guest reads their own order with a capability token, not a login**: `GET /api/orders/{id}` is `[Authorize(Roles = "Owner,Customer")]` and stays that way — order ids are sequential, so anonymous access there would let anyone walk every customer's name, phone and address. Instead `Orders.PublicToken` (a non-nullable `Guid`, v4, unique-indexed) is generated on the entity at creation and returned **only** in `OrderCreateResponseDto` — the one response that provably goes to the person who placed the order. `GET /api/orders/by-token/{token:guid}` is `[AllowAnonymous]`: holding 122 random bits *is* the authorisation, and there is no id space to enumerate. The `{token:guid}` route constraint means a malformed token 404s at routing without reaching the database.
- **`GuestOrderDto` is a deliberately narrower projection than `OrderDto`**, because anyone holding the link sees all of it. Rule applied: include what the confirmation page renders, exclude what is decoration. `CustomerPhone` and `CustomerName` are **absent**; `DeliveryAddress` is present, because checking the food is going to the right place is a real function of the page. `CustomerId`/`DriverId` are absent as internal ids. `PublicToken` is **never** a field on `OrderDto` — that shape feeds the Owner's whole-restaurant list, which would hand out every order's key at once.
- The token is the guest's **only** route back to their order, and it exists exactly once, in the create response. The frontend carries it checkout → `/pay` → `/order/:token`; putting it in the URL (not sessionStorage) is what makes the page bookmarkable, refreshable and alive after a tab close.
- The confirmation page **polls** this endpoint every 5s rather than using WebSockets (still out of scope). The wait it actually exists for is the webhook one: `confirmPayment` resolving does not mean the order is `Paid`, so a fetch-once page would routinely render `Pending` and look broken. Polling stops on `Completed`/`Cancelled` and on unmount.
- Backfill note: the `AddOrderPublicToken` migration is **hand-edited**. EF's `AddColumn` gives every pre-existing row the same all-zeros default, so the unique index cannot be created until a `migrationBuilder.Sql` `UPDATE ... gen_random_uuid()` runs between the two steps.

**Order modifiers are structured and priced, not free text**: Static, priced options per menu item — "Add peppers +£0.50", "Remove onions", "Extra chicken +£1.50" — rather than a notes field for anything that affects price or kitchen prep. Schema, following the same price-snapshotting principle as `OrderItems`:
- `ModifierGroup` — e.g. "Sauces", "Extra Toppings". Has `MinSelect` / `MaxSelect` (e.g. 0/1 for "pick one sauce", 0/many for "extra toppings") and `IsRequired`. Reusable, not owned by a single menu item.
- `MenuItemModifierGroup` — join table linking `MenuItem` ↔ `ModifierGroup`, since a group like "Sauces" applies across many menu items, and one item can offer several groups.
- `ModifierOption` — e.g. "Peppers", "No Onions", "Extra Chicken". Belongs to a `ModifierGroup`. Has `Name`, `PriceDelta` (decimal — can be 0 for a "remove" option), `IsActive`.
- `OrderItemModifiers` — snapshot table, same pattern as `OrderItems`: copies the option's `Name` and `PriceDelta` at order time. Never just a FK to `ModifierOption`.
- Order total becomes: `sum((OrderItem.UnitPrice + sum(its OrderItemModifiers.PriceDelta)) * Quantity)`. Modifiers are priced **per portion**: an order line means "`Quantity` identical portions, each made with these options", which is how the kitchen reads "3 × Pizza, Extra cheese" — three pizzas, all with extra cheese — so the cheese is charged three times. `OrderItemModifier` needs no quantity column for this; the line's `Quantity` covers it.
- History: until Nov 2026 deltas were added **once per line** (2 portions with "Extra chicken +£1.50" added £1.50), which undercharged every multi-portion line with a priced option. Changing it was a formula change, not a schema change — the real obstacle was that totals used to be recomputed on every read, which `Order.Total` (a stored snapshot) removed. Orders placed before the switch keep their stored, per-line totals; only their receipt's *per-line* breakdown (computed by `lineTotal`) can disagree with the stored total, on lines with quantity > 1 and a priced option.
- **The frontend must mirror `ComputeTotal`, and does so in exactly one function**: `lineTotal` in `utils/basket.js`, used by the basket, checkout, the menu item's "Add to basket" button *and* the confirmation page's receipt. Don't reintroduce a second copy of this calculation.
- A short optional free-text notes field stays alongside the structured modifiers, for one-off requests that don't fit a static option — kitchens always get something that doesn't fit a dropdown. unsure how to price this yet.

**Owner account bootstrapping is self-closing, not env-seeded**: There's no separate "create the first Owner" step. `POST /api/auth/register` accepts `role: "Owner"` unauthenticated, but only while zero Owner accounts exist — `AuthService` checks `UserManager.GetUsersInRoleAsync(Roles.Owner)` before allowing it. The moment the first Owner is created, that branch rejects every future attempt with a 403. There's deliberately no route to add a second Owner afterward — if the business ever needs more than one, that's a gap to solve later, not an oversight.

**Driver accounts are Owner-created, not self-registered**: Drivers don't sign themselves up through a public form. The same `POST /api/auth/register` endpoint accepts `role: "Driver"`, but only succeeds if the caller is already authenticated as an Owner — checked via the caller's JWT role claim (`User.IsInRole(Roles.Owner)` in `AuthController`), not a flag in the request body. There is no standalone `POST /api/drivers` create endpoint; a Driver's `ApplicationUser` login and `Driver` profile row are always created together in one `AuthService.RegisterAsync` call.

**JWTs only, no refresh tokens in v1**: `/api/auth/login` and `/api/auth/register` return a JWT (role claim included) with a **30-day** expiry (raised from 1 day in Aug 2026, so a kitchen tablet or a demo visitor isn't logged out daily). There's no refresh-token flow — once it expires, the user just logs in again. The cost of the long lifetime: a token can't be revoked, so a lost owner tablet stays signed in until it expires (deleting a Driver does cut them off, since every driver endpoint resolves the `Driver` row). Acceptable for one shop; revisit with refresh tokens or a security-stamp check before multiple staff accounts exist.

## V1 Scope — Do Not Exceed Without Asking
**In scope:**
- Customer: browse menu, build basket, submit order (name/phone/address/notes)
- Optional customer accounts — registration/login, saved addresses, order history; guest checkout remains fully supported and is the default
- Guest order confirmation/tracking via a per-order capability token (see Architecture Decisions), so a guest can follow their order without an account
- Structured order modifiers with per-option pricing (see Architecture Decisions), plus a short optional free-text notes field
- No `Staff` role/account for v1 — Owner covers order viewing/status updates for now. Revisit if the business needs a separate lower-privilege staff login.
- Owner: log in, add/edit/disable menu items and categories, view orders, update order status
- Driver role and driver dashboard
- Real payment processing (Stripe)
- Enforce business hours — reject order submission outside opening hours, with a manual override for holidays/closures
- Owner-configurable delivery fee, snapshotted onto the order at creation (see Architecture Decisions)
- Delivery radius by postcode — reject a delivery order to an address outside the allowed postcodes

**Out of scope for v1 (do not build unless asked):**
- Real-time updates (WebSockets/SignalR)
- Reporting or analytics
- Email or SMS notifications
- ML features (demand forecasting, delivery estimates)

## Code Conventions
- C#: follow standard .NET naming (PascalCase types and methods, camelCase locals)
- Use `async/await` throughout — no blocking `.Result` or `.Wait()` calls
- Return `ActionResult<T>` from controllers, not raw objects
- Use `decimal` for all money/price fields — never `float`/`double` — to avoid rounding errors in totals
- React: functional components only, `useState`/`useEffect` hooks
- Basket state lives in a `BasketContext` (React Context + `useReducer`), persisted to `localStorage` so it survives a page refresh — Redux/Zustand would be overkill for this app's scope
- Frontend API calls live in `/src/api/`, not inline in components
- Never commit secrets, connection strings, or `.env` files — use environment variables

## Environment Variables (never hardcode these)
```
DATABASE_URL                 → Postgres connection string
JWT_SECRET                   → JWT signing key (min 32 chars)
ASPNETCORE_ENVIRONMENT       → Development | Production
VITE_API_URL                 → Base URL of the backend API (used by frontend)
STRIPE_SECRET_KEY            → Stripe secret API key (server-side)
STRIPE_WEBHOOK_SECRET        → Verifies incoming Stripe webhook signatures
VITE_STRIPE_PUBLISHABLE_KEY  → Stripe publishable key (frontend, safe to expose)
RESTAURANT_TIMEZONE          → IANA timezone for business-hours checks (e.g. Europe/London)
```
