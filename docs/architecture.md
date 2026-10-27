# Architecture

CreativeCollab is one API and, when the frontend step lands, one Angular app. The API owns the Creative. The browser owns the Draft. A save sends the Version the Collaborator loaded. The API accepts it or returns a Conflict.

This page separates what the repository runs today from what [PLAN.md](../PLAN.md) still requires. A diagram of the target is not a claim that the target is built.

## What runs today

```
HTTP client
    │
    ▼
Minimal API  /api/creatives
    │  list, get, create, update
    ▼
CreativeService
    │
    ├──▶ IHubContext<CreativeHub> ──▶ SignalR  /hubs/creatives  (PresenceTracker)
    ▼
EfCreativeRepository
    │
    ▼
SQLite  creativecollab.db
```

| Piece | Where | Behaviour |
|---|---|---|
| Host | `backend/CreativeCollab.Api/Program.cs` | Wires TimeProvider, the DbContext, the image-check client, the repository, the service, and FluentValidation. In Development only, applies migrations and seeds |
| HTTP | `Endpoints/CreativeEndpoints.cs` | `GET /api/creatives`, `GET /api/creatives/{id}`, `POST /api/creatives`, `PUT /api/creatives/{id}` |
| Rules | `Services/CreativeService.cs` | Creates a Creative at Version 1. Update checks the supplied Version, increments it, stamps `updatedAt` from TimeProvider, records the Display name. A stale Version, or a store that rejects the compare-and-swap, is a Conflict carrying the latest Creative |
| Store | `Data/EfCreativeRepository.cs`, `Data/CreativeDbContext.cs` | EF Core 10 on SQLite. The connection string `ConnectionStrings:Creatives` defaults to `Data Source=creativecollab.db` (gitignored). Version is a concurrency token, so a save that loses a race raises `DbUpdateConcurrencyException`, which the repository reports as not updated and the service turns into the same Conflict. `updatedAt` is stored as UTC ticks so it sorts. Scoped lifetime |
| Hub | `Hubs/CreativeHub.cs`, `Hubs/PresenceTracker.cs` | `/hubs/creatives`. `JoinCreative(id, userName)` returns the Display names present (one per connection, sorted) and sends `UserJoined` to the others in group `creative-{id}`. `LeaveCreative(id)` and a dropped connection send `UserLeft` to the others. A blank name is a `HubException`. The tracker is a singleton keyed by connection id with no scoped dependencies. Single instance, no backplane: ADR 0002 |
| Broadcast | `Services/CreativeService.cs` | After a successful update the service sends `CreativeUpdated` (the full Creative) to the group through `IHubContext<CreativeHub>`, sender included. A failed push is logged and does not fail the save. Create does not broadcast |
| Image check | `Services/ImageUrlValidator.cs`, `Services/PublicAddress.cs` | Typed client from `IHttpClientFactory` (pooled handler, no redirects, 3 s timeout from `ImageValidation:Timeout`). Runs on create and on update only when the image URL is new or changed, after the Version check. Resolves the host, refuses any non-public address, sends HEAD, then a one-byte ranged GET if HEAD is not successful. Only `image/*` passes. Every failure is a 400 on `imageUrl`. DNS rebinding is not fully mitigated: the host is resolved again when the client connects |
| Errors | `Infrastructure/GlobalExceptionHandler.cs` | An unhandled exception is logged and answered as a 500 `ProblemDetails` with a generic title and no detail, in every environment. Request-binding errors such as malformed JSON keep their 4xx |
| CORS | `Program.cs`, `Cors:AllowedOrigins` in `appsettings.json` | Default policy for `http://localhost:4200` with credentials. Any other origin gets no CORS headers |
| Migrations and seed | `Data/Migrations/`, `Data/DatabaseInitializer.cs` | Development only: migrate on startup, then add three Creatives with empty `updatedBy` if the table is empty |
| Validation | `Validation/` | Create and update share one rule set; update also requires Version >= 1. Empty required fields, max lengths, and a non-http(s) image URL return 400 `ValidationProblemDetails` |
| Tests | `backend/CreativeCollab.Api.Tests` | xUnit and `WebApplicationFactory` on in-memory SQLite with the real migrations. A fake TimeProvider controls time. A save interceptor stages a genuine race. A file database and two hosts prove a restart |

A stale update returns 409 `ProblemDetails` with the latest Creative in `current`. There is no `frontend/` directory in this tree.

Development listens on `http://localhost:5114` (`Properties/launchSettings.json`).

```bash
dotnet test backend/CreativeCollab.slnx
dotnet run --project backend/CreativeCollab.Api
```

## Target shape

The plan adds the pieces below in order. Each one stays behind the service. Endpoints stay thin.

```
Angular (localhost:4200)
    CreativesApi          HTTP
    CreativeStore         Signals: list, selected, loading, error, conflict
    CreativeHubService    SignalR
            │
            ▼
API
    ValidationFilter          400
    CreativeService
        pre-check Version
        increment and save
        broadcast CreativeUpdated
            │
            ├── EF Core + SQLite (creativecollab.db)
            ├── IHubContext<CreativeHub>     group creative-{id}
            ├── Presence tracker             in-memory, one process
            └── ImageUrlValidator            HEAD, then a ranged GET
```

| Concern | Rule |
|---|---|
| Concurrency | Integer Version, not a rowversion. Stale save and `DbUpdateConcurrencyException` both return 409 with the latest Creative in `current`. See [ADR 0001](adr/0001-optimistic-concurrency-with-integer-version.md) |
| Live updates | Hub `/hubs/creatives`. `JoinCreative`, `LeaveCreative`. Events `CreativeUpdated`, `UserJoined`, `UserLeft`. The list page is not live |
| Presence | Singleton `ConcurrentDictionary` keyed by connection id. Lost on restart. One server instance. See [ADR 0002](adr/0002-single-instance-in-memory-presence.md) |
| Image | Only when `imageUrl` changes. `image/*` only. Three-second timeout. No redirects. Loopback, private, and link-local addresses are rejected. Failure is 400. DNS rebinding is not fully closed |
| Identity | Display name from the client. Trusted, not authenticated. Required on create and update |
| CORS | `http://localhost:4200` with credentials, because SignalR needs it. The Angular app calls the API directly |
| Frontend | Angular 22, standalone, Signals, OnPush. Containers hold state. Presentational components take `input()` and `output()` only. A dirty form is never overwritten by `CreativeUpdated` |

## Folders

| Path | May do | Must not do |
|---|---|---|
| `Endpoints/` | Map routes, translate results | Hold business rules |
| `Services/` | Version check, save, broadcast, image check | Talk HTTP details or know about Angular |
| `Data/` | Persist and query Creatives | Validate input or broadcast |
| `Hubs/` | Group membership and Presence | Save a Creative |
| `Validation/` | Shape of a request | Decide a Conflict |
| `frontend/` (not created) | Draft, Conflict UI, hub client | Invent a second Version rule |

No second backend project. The test project is the only sibling of the API.

## Failure rules already fixed

| Situation | Response |
|---|---|
| Unknown id | 404 |
| Invalid create body | 400 `ValidationProblemDetails` |
| Stale Version | 409, body includes `current` |
| Image URL that is not a public image | 400 on `imageUrl` |
| Unexpected exception | 500 ProblemDetails with no internals |

There is no delete.
