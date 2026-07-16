# TakeawayOS
An online ordering and order management system for an independent takeaway restaurant.  
The system will be deployed and used by a real business, replacing telephone-only ordering with a live web platform.

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

## V1 Scope 
- Customer: browse menu, build basket, submit order (name/phone/address/notes)
- Optional customer accounts — registration/login, saved addresses, order history; guest checkout remains fully supported and is the default
- Structured order modifiers with per-option pricing (see Architecture Decisions), plus a short optional free-text notes field
- No `Staff` role/account for v1 — Owner covers order viewing/status updates for now. Revisit if the business needs a separate lower-privilege staff login.
- Owner: log in, add/edit/disable menu items and categories, view orders, update order status
- Driver role and driver dashboard
- Real payment processing (Stripe)
- Enforce business hours — reject order submission outside opening hours, with a manual override for holidays/closures

**Out of scope for v1 **
- Real-time updates (WebSockets/SignalR)
- Reporting or analytics
- Email or SMS notifications
- ML features (demand forecasting, delivery estimates)


