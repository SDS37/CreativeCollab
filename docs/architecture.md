# Architecture

CreativeCollab is one API and one Angular app. The API owns the Creative. The browser owns the Draft. A save sends the Version the Collaborator loaded. The API accepts it or returns a Conflict.

This page separates what the repository runs today from what [PLAN.md](../PLAN.md) still requires. A diagram of the target is not a claim that the target is built.

## What runs today

```
HTTP client
    │
    ▼
Minimal API  /api/creatives
    │  list, get, create
    ▼
CreativeService
    │
    ▼
InMemoryCreativeRepository
```

| Piece | Where | Behaviour |
|---|---|---|
| Host | `backend/CreativeCollab.Api/Program.cs` | Wires TimeProvider, the repository, the service, and FluentValidation |
| HTTP | `Endpoints/CreativeEndpoints.cs` | `GET /api/creatives`, `GET /api/creatives/{id}`, `POST /api/creatives` |
| Rules | `Services/CreativeService.cs` | Creates a Creative at Version 1, stamps `updatedAt` from TimeProvider, records the Display name |
| Store | `Data/InMemoryCreativeRepository.cs` | Process memory. The list is most recently updated first. Restart drops every Creative |
| Validation | `Validation/` | Create only. Empty required fields, max lengths, and a non-http(s) image URL return 400 `ValidationProblemDetails` |
| Tests | `backend/CreativeCollab.Api.Tests` | xUnit and `WebApplicationFactory`. A fake TimeProvider controls sort order |

There is no update route, no 409, no database file, and no hub on the backend branch this was written against. The backend steps 2 to 6 are built elsewhere.

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

## Frontend today

`frontend/` is an Angular 22 app (standalone, Signals, no NgRx). Angular 22 makes OnPush the default, so no component sets it. Tests run on Vitest through `ng test`, with HTTP mocked by `HttpTestingController`, so no API is needed. The API is called directly at `http://localhost:5114` (`core/api-config.ts`).

```bash
cd frontend && npm install && npm start     # http://localhost:4200
cd frontend && npm test -- --watch=false
```

| Piece | Where | Behaviour |
|---|---|---|
| `DisplayName` | `core/display-name.ts` | The Collaborator's Display name in localStorage. Trusted, not authenticated. The shell shows a prompt until it is set and lets it be changed |
| `CreativesApi` | `core/creatives-api.ts` | One method per endpoint. Adds the Display name to create and update, and the loaded Version to update. An empty image URL is omitted |
| `CreativeStore` | `core/creative-store.ts` | Signals: `creatives`, `selected`, `loading`, `saving`, `error`, `conflict`. A 409 with `current` sets `conflict` (not `error`) and leaves `selected` as the Version the Collaborator loaded. `loadLatest` adopts `conflict`; `keepMyEdits` Rebases and saves with `current.version`. Turns HTTP failures into network, not-found, validation (field messages) or server errors. A newer load cancels an older one |
| `CreativeListPage`, `CreativeEditPage` | `creatives/` | Containers. The edit page serves `/creatives/new` and `/creatives/:id`. They show loading, error with retry, and the form |
| `Rebase` | `core/rebase.ts` | Pure function: fields where the Draft differs from the loaded Creative keep the Draft value, all others take the latest. An empty and an absent image URL are equal |
| `creative-list`, `creative-form`, `conflict-banner`, `display-name-prompt` | `creatives/`, `display-name-prompt/` | Presentational: `input()` and `output()` only. The form emits its Draft as it changes and resets only when handed a different Creative |

On a Conflict the Draft stays in the form and the banner names who saved (`current.updatedBy`). It offers Load latest and Keep my edits, with no merge or diff UI.

Not built yet in the frontend: the hub client with Presence and live updates (issue 9).

## Folders

| Path | May do | Must not do |
|---|---|---|
| `Endpoints/` | Map routes, translate results | Hold business rules |
| `Services/` | Version check, save, broadcast, image check | Talk HTTP details or know about Angular |
| `Data/` | Persist and query Creatives | Validate input or broadcast |
| `Hubs/` | Group membership and Presence | Save a Creative |
| `Validation/` | Shape of a request | Decide a Conflict |
| `frontend/` | Draft, Conflict UI, hub client | Invent a second Version rule |

No second backend project. The test project is the only sibling of the API.

## Failure rules already fixed

| Situation | Response |
|---|---|
| Unknown id | 404 |
| Invalid create body | 400 `ValidationProblemDetails` |
| Stale Version, once update exists | 409, body includes `current` |
| Image URL that is not a public image, once the checker exists | 400 |
| Unexpected exception, once the handler exists | ProblemDetails, not an empty 500 |

There is no delete.
