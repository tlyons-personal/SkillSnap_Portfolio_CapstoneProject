# SkillSnap.Api

The backend REST API for **SkillSnap**, a portfolio-management application where users showcase their projects and skills. Built with ASP.NET Core (`net10.0`), Entity Framework Core (SQLite), and ASP.NET Core Identity with JWT authentication.

## Overview

`SkillSnap.Api` exposes a secured Web API that lets users register, authenticate, and manage portfolio profiles, projects, and skills. It enforces role-based access control, throttles authentication endpoints, and uses in-memory caching to reduce database load.

## Tech Stack

- **Framework:** ASP.NET Core Web API (.NET 10)
- **Data:** Entity Framework Core + SQLite (`skillsnap.db`)
- **Auth:** ASP.NET Core Identity + JWT bearer tokens
- **Caching:** `IMemoryCache` (per-user cache buckets, 5-minute expiry)
- **Docs:** OpenAPI (enabled in Development)
- **Shared:** References `SkillSnap.Shared` for models and DTOs

## Key Features

- **Authentication & JWT issuance** — registration, login, and short-lived (15 min) signed tokens.
- **Role-based authorization** — three roles (`User`, `Manager`, `Admin`) with policies:
  - `CanReadAll` — Managers and Admins can read across all users.
  - `AdminOnly` — Admin-restricted operations (e.g. updates, portfolio management).
- **Account security** — strong password rules, account lockout (5 attempts / 15 min), rate limiting on auth endpoints (5 requests/min), and generic login responses to prevent user enumeration.
- **Portfolio management** — users manage their own profile, projects, and skills; Admins manage any.
- **Caching with invalidation** — read endpoints are cached per user and invalidated on create/update/delete.
- **Database seeding** — one-time seed of a demo admin user with sample projects and skills.

## Project Structure

```
SkillSnap.Api/
├── Controllers/
│   ├── ApiControllerBase.cs        # Shared current-user/role helpers
│   ├── AuthController.cs           # Register, login, promote, delete accounts
│   ├── PortfolioUserController.cs  # Portfolio profile CRUD
│   ├── ProjectsController.cs       # Project CRUD (cached)
│   ├── SkillsController.cs         # Skill CRUD (cached)
│   └── SeedController.cs           # One-time demo data seeding
├── Models/                         # ApplicationUser, JwtOptions, Roles, DbContext, etc.
├── Migrations/                     # EF Core migrations
├── Program.cs                      # App startup, DI, auth, CORS, rate limiting
└── appsettings.json                # Connection string, JWT, CORS config
```

## API Endpoints

### Auth — `/api/auth`
| Method | Route | Access | Description |
|--------|-------|--------|-------------|
| POST | `/register` | Anonymous (rate-limited) | Create a new user + linked portfolio |
| POST | `/login` | Anonymous (rate-limited) | Validate credentials, return a JWT |
| GET | `/users` | Admin | List users with roles and display names |
| POST | `/promote` | Admin | Promote a user to Manager or Admin |
| DELETE | `/me` | Authenticated | Delete the current user's own account |
| DELETE | `/users/{userName}` | Admin | Delete another user's account |

### Portfolio Users — `/api/portfoliouser`
| Method | Route | Access | Description |
|--------|-------|--------|-------------|
| GET | `/` | Authenticated | List all portfolio profiles |
| GET | `/me` | Authenticated | Get the current user's profile |
| PUT | `/me` | Authenticated | Update the current user's profile |
| GET | `/{id}` | Authenticated | Get a profile by id |
| POST | `/` | Admin | Create a portfolio profile |
| PUT | `/{id}` | Owner or Admin | Update a profile |
| DELETE | `/{id}` | Admin | Delete a profile (cascades projects/skills) |

### Projects — `/api/projects`
| Method | Route | Access | Description |
|--------|-------|--------|-------------|
| GET | `/` | Authenticated | List own projects (all, for Managers/Admins) |
| GET | `/user/{portfolioUserId}` | Manager/Admin | List a specific user's projects |
| GET | `/{id}` | Authenticated | Get a project by id |
| POST | `/` | Authenticated | Create a project for own portfolio (Admin: any) |
| PUT | `/{id}` | Admin | Update a project |
| DELETE | `/{id}` | Admin | Delete a project |

### Skills — `/api/skills`
| Method | Route | Access | Description |
|--------|-------|--------|-------------|
| GET | `/` | Authenticated | List own skills |
| GET | `/user/{portfolioUserId}` | Manager/Admin | List a specific user's skills |
| GET | `/{id}` | Authenticated | Get a skill by id |
| POST | `/` | Authenticated | Create a skill for own portfolio |
| PUT | `/{id}` | Admin | Update a skill |
| DELETE | `/{id}` | Owner or Admin | Delete a skill |

### Seed — `/api/seed`
| Method | Route | Access | Description |
|--------|-------|--------|-------------|
| POST | `/` | Anonymous | Insert demo data (runs only when empty) |

## Configuration

Configured via `appsettings.json`:

- `ConnectionStrings:DefaultConnection` — SQLite database path.
- `Cors:AllowedOrigins` — allowed client origins (e.g. the Blazor client).
- `Jwt` — signing `Key`, `Issuer`, `Audience`, and `ExpiryMinutes`.

> **Security note:** The JWT signing key in `appsettings.json` is a development placeholder. Store secrets outside source control (User Secrets, environment variables, or a key vault) for any real deployment.

## Getting Started

```bash
# Restore and build
dotnet restore
dotnet build

# Apply migrations / create the database
dotnet ef database update --project SkillSnap.Api

# Run the API
dotnet run --project SkillSnap.Api
```

Once running, seed demo data with a POST to `/api/seed`. In Development, OpenAPI documentation is available at the mapped OpenAPI endpoint.

### Demo Admin Credentials (after seeding)

- **Username:** `jordan@skillsnap.dev`
- **Password:** `AdminTad#12345`
