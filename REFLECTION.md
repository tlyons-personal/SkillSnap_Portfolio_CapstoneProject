# SkillSnap — Capstone Reflection

## Application Overview

SkillSnap is a full-stack portfolio management application built on .NET 10. It lets users register an account and showcase a personal portfolio of **projects** and **skills**, while Managers and Admins get elevated access to browse all portfolios and manage accounts. The solution is split into three projects: an **ASP.NET Core Web API** backend (EF Core + SQLite, Identity, JWT), a **Blazor WebAssembly** SPA frontend, and a **shared class library** holding the domain models and DTOs referenced by both ends.

Three key features:

1. **JWT authentication with role-based access** — the API issues short-lived signed tokens carrying role claims (`User`, `Manager`, `Admin`), and those roles are enforced consistently from the controller down.
2. **Scoped portfolio management** — regular users manage only their own profile, projects, and skills, while Managers/Admins can read across every user's portfolio through dedicated endpoints and policies.
3. **Server-side caching with targeted invalidation** — read-heavy project and skill queries are served from an in-memory cache keyed per user, and writes surgically evict only the buckets they affect.

## Business Logic, Data Persistence, and State Management

**Business logic** lives in the API controllers, which inherit a small `ApiControllerBase` that centralizes identity concerns (`CurrentUserId`, `IsAdmin`, `CanReadAll`). This keeps authorization decisions declarative and consistent — for example, `GetProjects` branches on `CanReadAll` to decide whether to return the full list or scope the query to the caller's own portfolio. Multi-step operations that must be atomic (registration creating both an Identity user and a linked `PortfolioUser`, or account deletion removing the login and cascading its portfolio) are wrapped in explicit EF Core transactions with rollback on failure.

**Data persistence** uses EF Core over SQLite with a code-first, migration-driven schema. The model is a one-to-one link between the Identity `ApplicationUser` and a `PortfolioUser`, which in turn owns collections of `Project` and `Skill` records with cascade delete configured. Read queries consistently use `AsNoTracking()` since the data is returned as DTOs rather than mutated, and projections to flat DTOs (via a reusable `Expression<Func<Project, ProjectDto>>`) avoid serializing navigation cycles and over-fetching columns.

**State management** is split by layer. On the server, state is stateless per request — identity is reconstructed from the bearer token each call, which keeps the API horizontally scalable. On the client, a scoped `UserSessionService` caches loaded projects and skills so components share data instead of each re-fetching, and authentication state flows through a custom `JwtAuthenticationStateProvider` that parses the stored token into a `ClaimsPrincipal` and notifies Blazor on login/logout.

## Security

**Validation** happens at the boundary: every auth action checks `ModelState.IsValid` before touching the database, username uniqueness is verified case-insensitively, and promotion requests are constrained to the two elevated roles. Identity enforces strong password rules centrally (12-character minimum, mixed case, digits, symbols, 4 unique characters).

**Authentication** is handled by ASP.NET Core Identity. Login uses `CheckPasswordSignInAsync` with `lockoutOnFailure: true`, which enforces account lockout (5 failed attempts, 15-minute window) without issuing a cookie. Login responses are deliberately generic ("Invalid username or password") whether the user is missing or the password is wrong, to prevent user enumeration, and the auth endpoints are rate-limited (fixed window, 5 requests/minute) to blunt brute-force and credential-stuffing attacks.

**Token handling** uses signed JWTs carrying `sub`, `nameid`, `name`, and role claims. The API validates issuer, audience, lifetime, and signing key on every request with a tight 30-second clock skew. On the client, a `DelegatingHandler` attaches the bearer token to every outgoing request automatically, and the state provider proactively checks token expiry, clearing expired tokens and reverting to anonymous.

**Authorization** is layered: `[Authorize]` secures controllers by default, named policies (`CanReadAll` for Managers/Admins, `AdminOnly`) gate elevated endpoints, and ownership checks (e.g., delete is allowed for Admins *or* the portfolio owner) enforce row-level access. CORS is restricted to configured origins rather than wildcarded.

## Performance and Caching

The main performance work is **server-side in-memory caching** (`IMemoryCache`) over read-heavy project and skill endpoints, with a 5-minute sliding window. Cache keys are deliberately partitioned — `projects_all` for the Manager/Admin view versus `projects_{userId}` for an individual's list — so each audience gets a correctly scoped result. Writes perform **targeted invalidation**: creating, updating, or deleting a project evicts only the specific per-user bucket, the shared list, and the single-item key, rather than flushing the whole cache. Query-level optimizations (`AsNoTracking`, DTO projection to select only needed columns, server-side filtering before materialization) reduce both database and serialization cost. On the client, the `UserSessionService` avoids redundant round-trips within a session.

**Tradeoffs and what I would add:** In-memory caching is simple and fast but node-local, so it won't stay consistent across multiple API instances — a distributed cache (Redis) would be the next step for scale-out, at the cost of added infrastructure and network latency. The current 5-minute TTL plus manual invalidation risks brief staleness and is easy to under-invalidate as endpoints grow; a tagged/dependency-based cache or cache-aside with versioned keys would make invalidation more robust. Longer term I would add pagination to the "all portfolios" endpoints (currently unbounded), output caching with ETags for conditional GETs, and response compression — each trading a little complexity for meaningful reductions in payload size and database load.

## Development Challenges

**Extending beyond the capstone scope.** The project started from the capstone's baseline directions, but I chose to expand it well past that foundation — most significantly by introducing multiple layers of users who can each access different areas of the app. Fleshing out that new functionality meant more than adding endpoints: it required rethinking the data structures and reworking existing code to support the new role-driven workflow end to end (the `ApplicationUser`-to-`PortfolioUser` link, role claims, scoped queries, and policy-gated endpoints all grew out of this). The effort took longer than I first anticipated, since each new capability rippled through the models, controllers, and client. Working through questions with Copilot — both for design direction and for resolving concrete coding issues — let me land the extended multi-tier access model cleanly rather than bolting it on.

**Blazor structure and styling.** The second challenge was the UI itself. I consider myself more logical than artsy, so designing a layout that both looks pleasant and *flows* well for the user pushed me outside my comfort zone. Getting the page structure, navigation, and visual styling to feel cohesive took real trial and error. Leaning on Copilot for suggestions and iterating on them, I arrived at what I think is a clean, intuitive flow with a pleasant appearance — a UI that supports the app's workflow instead of fighting it.
