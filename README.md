# Takeaway-OS



An online ordering and order management system for an independent takeaway restaurant. Built as a portfolio project by a pre-university student. The system will be deployed and used by a real business replacing telephone-only ordering with a live web platform.



\---



\## Tech Stack



| Layer | Choice |

|---|---|

| Backend | ASP.NET Core Web API, .NET 10, C# |

| ORM | Entity Framework Core 10 (Npgsql provider) |

| Database | PostgreSQL |

| Auth | ASP.NET Core Identity + JWT |

| Frontend | React (JavaScript), Vite |

| Version Control | Git + GitHub |

| Hosting | Render (API + Postgres) + Vercel (frontend) |



\---



\## Project Structure



```

/TakeawayOS

&#x20; /TakeawayOS.API          → ASP.NET Core Web API project

&#x20;   /Controllers           → HTTP endpoints

&#x20;   /Models                → EF Core entity classes

&#x20;   /DTOs                  → Request/response shapes (never expose raw entities)

&#x20;   /Data                  → DbContext, migrations

&#x20;   /Services              → Business logic, kept out of controllers

&#x20; /TakeawayOS.Frontend     → React + Vite frontend

&#x20;   /src

&#x20;     /components          → Reusable UI components

&#x20;     /pages               → Route-level page components

&#x20;     /api                 → Fetch wrapper functions for calling the backend

