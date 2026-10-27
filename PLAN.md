# CreativeCollab — Implementation Plan

A small real-time collaborative editor for ad creatives. It handles concurrent edits with optimistic concurrency and SignalR, and it demonstrates senior-level code-quality patterns.

Status: **plan agreed. Steps 1–6 (list, get, create, update with 409, SQLite, SignalR, image check, error handling and CORS) are in `backend/`. Steps 7–8 are not started.** The map of what that means is [docs/architecture.md](docs/architecture.md). Done versus remaining is [docs/DoD.md](docs/DoD.md).

## Decisions (from design review)

### Repo and tooling
- Monorepo: `/backend` and `/frontend`.
- Backend is one API project plus one test project, organised in folders (`Endpoints/`, `Services/`, `Data/`, `Hubs/`, `Validation/`). No multi-project clean-architecture split.
- .NET SDK 10.0.x is installed. Node 24 is the nvm default, which is what Angular 22 needs. The frontend is still not scaffolded. The backend is built first so nothing is blocked.
- No commits are made unless the user asks.
- Tests are written alongside each step, not strict TDD.

### Backend
- Minimal APIs, grouped in one `MapGroup("/api/creatives")` extension. Endpoints stay thin and delegate to a service layer.
- Validation uses **FluentValidation**. One endpoint filter returns 400 `ValidationProblemDetails`.
  - `name`: required, max 100.
  - `headline`: required, max 40.
  - `body`: required, max 200.
  - `cta`: required, max 20.
  - `imageUrl`: optional, absolute http/https URL, max 2048.
  - `version`: required on update, ≥ 1.
  - New creatives start at version 1.
- No delete endpoint or UI (list / view / edit / create only).
- Everything is implemented correctly. There are no planted flaws.
- `updatedAt` comes from an injected `TimeProvider`.
- Identity: the user types a display name, stored in localStorage and sent with requests and the hub join. It is trusted and unauthenticated, and flagged as such in the notes. Light auth is out of scope.
- Every create/update requires a display name (400 if missing). `updatedBy` is empty only for seeded creatives.
- Creative `name` is not unique, and the list sorts by most recently updated.
- Vocabulary is defined in `GLOSSARY.md`; design rationale is in `docs/adr/`.

### Concurrency
- `Version` is an EF concurrency token (`IsConcurrencyToken`), since SQLite has no rowversion.
- The service pre-checks the client's version, then increments it and saves.
- A race surfaces as `DbUpdateConcurrencyException`. Both paths return the same **409** `ProblemDetails`.
- The 409 body includes the latest server copy of the creative in an extension field, `current`.
- Status codes: 404 missing id, 400 invalid input, 409 stale version.

### SignalR
- Hub at `/hubs/creatives` with `JoinCreative(id, userName)` and `LeaveCreative(id)`. Events: `CreativeUpdated`, `UserJoined`, `UserLeft`.
- One group per creative (`creative-{id}`).
- After a save, the service calls `IHubContext<CreativeHub>` to broadcast `CreativeUpdated` (the full DTO) to the group. The sender receives it too, and the client ignores it if the version already matches.
- Presence is a **singleton in-memory tracker** (`ConcurrentDictionary`, keyed by connection id). It has no scoped dependencies. `OnDisconnectedAsync` removes the connection and emits `UserLeft`.
- Limitation, to be documented: single instance only, no backplane.
- The list page gets no live updates.

### External HTTP call
- A typed `ImageUrlValidator` client through `IHttpClientFactory`. It runs in the service on create/update when `imageUrl` changes.
- It sends a HEAD request, falling back to a ranged GET if HEAD isn't allowed.
- It has a 3-second timeout and follows no redirects.
- It accepts only `image/*` content types.
- SSRF guard: http/https only. It resolves the hostname first and blocks loopback, private, link-local and other non-public addresses.
- Failures, including network errors, return 400 with a clear message, not 500.
- Documented caveat: DNS rebinding is not fully mitigated at this scope.

### Persistence
- EF Core 10 + SQLite, with migrations applied at startup in Development only.
- The DB file is `creativecollab.db` (gitignored).
- A seeder adds a few creatives if the table is empty.

### Cross-cutting
- Global exception handling via `IExceptionHandler` and ProblemDetails.
- CORS is restricted to `http://localhost:4200` (configurable in `appsettings`) with `AllowCredentials`, as SignalR requires. The Angular app calls the API directly, with no dev proxy.
- DI lifetimes are reviewed (no scoped service in a singleton). Async is correct throughout (no `.Result`, `.Wait()` or `async void`).

### Frontend (Angular 22, standalone, Signals, OnPush)
- Services:
  - `CreativesApi` wraps `HttpClient`.
  - `CreativeStore` holds Signals state: `creatives`, `selected`, `loading`, `error`, `conflict`.
  - `CreativeHubService` wraps the SignalR connection.
- Smart containers: `CreativeListPage` and `CreativeEditPage`. They are OnPush and use `takeUntilDestroyed`.
- Presentational components: `creative-list`, `creative-form` and `presence-bar`. They use `input()` and `output()` only.
- No NgRx, and no logic in templates.
- **409 UX:** the draft stays in the form. A banner says "X saved a newer version" with two actions: **Load latest** (discard my edits) and **Keep my edits** (rebase: only the fields the user actually changed are re-applied onto the latest version, other fields take the latest server values, then save again). There is no merge or diff UI.
- **Live update while dirty:** a `CreativeUpdated` event never overwrites the draft. It shows a non-blocking "updated by X, load latest?" notice and tracks the new server version. If the form is clean, it updates silently.
- Explicit loading and error states.

### Testing
- Backend: xUnit integration tests (`WebApplicationFactory`, in-memory SQLite) for 404/400/409, the version increment, and the image validator (stubbed handler).
- Frontend: a small number of service tests, using the runner Angular 22 scaffolds by default.
- No end-to-end tests.

## Build order

1. **Backend scaffold:** `backend/CreativeCollab.Api` and `backend/CreativeCollab.Api.Tests`, with the in-memory store behind a repository interface. This gives list / get / create / update.
2. **Concurrency:** the Version field and 409, including `current` in the body.
3. **EF Core + SQLite:** migrations, seeding, `TimeProvider`.
4. **SignalR:** the hub, the presence tracker, and broadcasts via `IHubContext`.
5. **External call:** `ImageUrlValidator` via `IHttpClientFactory`.
6. **Cross-cutting:** the FluentValidation filter, the global exception handler, CORS, and a DI-lifetime review.
7. **Frontend** (after the Node upgrade): the Angular 22 scaffold, then the services, containers and presentational components, then the loading, error and conflict UX.
8. **Docs:** `REVIEW_NOTES.md` mapping each reviewed pattern to file and line, plus run instructions in the README.

Each step ends with a passing build and tests.
