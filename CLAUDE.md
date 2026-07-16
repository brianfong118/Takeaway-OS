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
dotnet test                         # run tests
```
**Frontend (run from `/TakeawayOS.Frontend`)**
```
npm install       # install dependencies
npm run dev       # start Vite dev server
npm run build     # production build
npm run lint      # lint
```
---
## Architecture Decisions — Know These
These are deliberate choices, not defaults. If asked to change them, flag it first.

**Price snapshotting on orders**: When an order is created, copy `item name` and `unit price` into the `OrderItems` table at that moment. Do NOT store only a foreign key to `MenuItems`. This preserves historical order accuracy if menu prices change later.

**DTOs over raw entities**: Controllers never return EF Core entity objects directly. Always map to a DTO first. This prevents accidental data leaks and keeps the API contract stable.

**Services layer**: Business logic lives in `/Services`, not in controllers. Controllers handle HTTP concerns only (routing, request parsing, response codes).

**Order status is a fixed set of values**: `OrderStatus` enum — `Pending` (placed, awaiting payment), `Paid` (Stripe confirmed), `Preparing`, `Ready`, `OutForDelivery` (delivery orders only), `Completed`, `Cancelled`. Valid forward flow: `Pending → Paid → Preparing → Ready → (OutForDelivery →) Completed`. `Cancelled` is only reachable from `Pending` or `Paid` — once an order is `Preparing`, the kitchen has already started, so cancelling past that point needs a staff override rather than a plain cancel action.

**Payment confirmation comes from Stripe's webhook, not the frontend**: Never mark an order `Paid` because the browser says the payment succeeded — that request can be faked or lost on a bad connection. Stripe calls a dedicated endpoint on the API directly (e.g. `POST /api/webhooks/stripe`) when a payment actually completes. That handler must (1) verify the request came from Stripe using `STRIPE_WEBHOOK_SECRET` to check the signature header, and (2) be idempotent — Stripe can send the same event more than once, so check whether the order is already `Paid` (or track the Stripe event ID) before processing it again.

**Menu item "disable" = soft delete**: Disabling a menu item sets `IsAvailable = false` on the `MenuItems` row — it is never deleted. The public menu query filters `WHERE IsAvailable = true`; the owner's admin view shows everything, active or not, so items can be switched back on without losing their description, price, or image.

**CORS is an explicit allow-list, not a wildcard**: The frontend (Vercel) and backend (Render) are different origins, so browsers block requests between them unless the API explicitly allows it. Configure CORS in `Program.cs` with a specific list of allowed origins from config/env (e.g. `http://localhost:5173` in Development, the real Vercel URL in Production) — never `AllowAnyOrigin()` on an API that also handles auth tokens.

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

**Customer accounts are optional, not required**: Guest checkout (name/phone/address/notes, no login) stays fully supported and is the default path — accounts are additive on top of it, not a replacement. A `Customer` role sits alongside the `Owner`/`Driver` roles in Identity — there is no `Staff` role (see V1 Scope). `Orders.CustomerId` is a nullable FK — null for guest orders, populated for logged-in orders. Guest contact fields (name/phone/address) stay on `Orders` regardless of whether a `CustomerId` is present, since delivery/contact needs them either way. A registered customer gets saved addresses, order history, and faster repeat checkout.

**Order modifiers are structured and priced, not free text**: Static, priced options per menu item — "Add peppers +£0.50", "Remove onions", "Extra chicken +£1.50" — rather than a notes field for anything that affects price or kitchen prep. Schema, following the same price-snapshotting principle as `OrderItems`:
- `ModifierGroup` — e.g. "Sauces", "Extra Toppings". Has `MinSelect` / `MaxSelect` (e.g. 0/1 for "pick one sauce", 0/many for "extra toppings") and `IsRequired`. Reusable, not owned by a single menu item.
- `MenuItemModifierGroup` — join table linking `MenuItem` ↔ `ModifierGroup`, since a group like "Sauces" applies across many menu items, and one item can offer several groups.
- `ModifierOption` — e.g. "Peppers", "No Onions", "Extra Chicken". Belongs to a `ModifierGroup`. Has `Name`, `PriceDelta` (decimal — can be 0 for a "remove" option), `IsActive`.
- `OrderItemModifiers` — snapshot table, same pattern as `OrderItems`: copies the option's `Name` and `PriceDelta` at order time. Never just a FK to `ModifierOption`.
- Order total becomes: `sum(OrderItem.UnitPrice * Quantity) + sum(OrderItemModifiers.PriceDelta)`.
- A short optional free-text notes field stays alongside the structured modifiers, for one-off requests that don't fit a static option — kitchens always get something that doesn't fit a dropdown. unsure how to price this yet.

**Owner account bootstrapping is self-closing, not env-seeded**: There's no separate "create the first Owner" step. `POST /api/auth/register` accepts `role: "Owner"` unauthenticated, but only while zero Owner accounts exist — `AuthService` checks `UserManager.GetUsersInRoleAsync(Roles.Owner)` before allowing it. The moment the first Owner is created, that branch rejects every future attempt with a 403. There's deliberately no route to add a second Owner afterward — if the business ever needs more than one, that's a gap to solve later, not an oversight.

**Driver accounts are Owner-created, not self-registered**: Drivers don't sign themselves up through a public form. The same `POST /api/auth/register` endpoint accepts `role: "Driver"`, but only succeeds if the caller is already authenticated as an Owner — checked via the caller's JWT role claim (`User.IsInRole(Roles.Owner)` in `AuthController`), not a flag in the request body. There is no standalone `POST /api/drivers` create endpoint; a Driver's `ApplicationUser` login and `Driver` profile row are always created together in one `AuthService.RegisterAsync` call.

**JWTs only, no refresh tokens in v1**: `/api/auth/login` and `/api/auth/register` return a JWT (role claim included) with a 1-day expiry. There's no refresh-token flow — once it expires, the user just logs in again. Acceptable tradeoff for this app's scope; revisit only if that friction becomes a real complaint.

## V1 Scope — Do Not Exceed Without Asking
**In scope:**
- Customer: browse menu, build basket, submit order (name/phone/address/notes)
- Optional customer accounts — registration/login, saved addresses, order history; guest checkout remains fully supported and is the default
- Structured order modifiers with per-option pricing (see Architecture Decisions), plus a short optional free-text notes field
- No `Staff` role/account for v1 — Owner covers order viewing/status updates for now. Revisit if the business needs a separate lower-privilege staff login.
- Owner: log in, add/edit/disable menu items and categories, view orders, update order status
- Driver role and driver dashboard
- Real payment processing (Stripe)
- Enforce business hours — reject order submission outside opening hours, with a manual override for holidays/closures

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
