# Review notes

Where each reviewed pattern lives in the code. Line numbers are from the commit this file shipped in; search for the named symbol if they drift.

## Backend (`backend/CreativeCollab.Api`)

| Pattern | Where | What to look at |
|---|---|---|
| `IHttpClientFactory` | `Program.cs:18-20`, `Services/ImageUrlValidator.cs` | A typed client registered once. The handler is a pooled `SocketsHttpHandler` with `AllowAutoRedirect = false`. The validator never calls `new HttpClient()` |
| DI lifetimes | `Program.cs:13-32` | Singletons: `TimeProvider`, `IHostAddressResolver`, `PresenceTracker`. Scoped: `CreativeDbContext`, `ICreativeRepository`, `CreativeService`. The singleton tracker depends on nothing scoped |
| `async`/`await` | `Services/CreativeService.cs`, `Data/EfCreativeRepository.cs`, `Services/ImageUrlValidator.cs` | Every I/O method returns `Task` and takes a `CancellationToken`. No `.Result`, `.Wait()` or `async void` |
| REST status codes | `Endpoints/CreativeEndpoints.cs:17-48` | 200, 201 with `Location`, 400 `ValidationProblemDetails`, 404, 409 `ProblemDetails` carrying `current` |
| Optimistic concurrency | `Data/CreativeDbContext.cs:24`, `Services/CreativeService.cs:40-75`, `Data/EfCreativeRepository.cs:39` | `Version` is a concurrency token. A stale pre-check and a lost race (`DbUpdateConcurrencyException`) produce the same 409. See ADR 0001 |
| Validation | `Validation/ValidationFilter.cs` | One generic endpoint filter, FluentValidation rules shared by create and update |
| Error handling | `Infrastructure/GlobalExceptionHandler.cs` | `IExceptionHandler`. 500s carry no detail. Binding errors keep their 4xx |
| CORS | `Program.cs:23-28` | Explicit origins from `Cors:AllowedOrigins`, with credentials (SignalR needs them), never a wildcard |
| Injected clock | `Program.cs:13`, `Services/CreativeService.cs` | `TimeProvider`, replaced by `FakeTimeProvider` in tests |
| SignalR and Presence | `Hubs/CreativeHub.cs`, `Hubs/PresenceTracker.cs`, `Program.cs:44` | One group per Creative. Presence is in memory, keyed by connection id. See ADR 0002 |
| SSRF guard | `Services/ImageUrlValidator.cs`, `Services/PublicAddress.cs` | Resolve the host, refuse non-public addresses, no redirects, 3 s timeout, `image/*` only |

## Frontend (`frontend/src/app`)

| Pattern | Where | What to look at |
|---|---|---|
| Signals state | `core/creative-signal-store.ts` | `signalStore` state: `creatives`, `selected`, `loading`, `saving`, `error`, `conflict`, `remoteUpdate` |
| OnPush | every component | Angular 22 makes OnPush the default, so components do not set it explicitly |
| Subscription hygiene | `creatives/creative-edit-page/creative-edit-page.ts:60,71,75`, `creatives/creative-form/creative-form.ts:51,55` | Route and hub subscriptions use `takeUntilDestroyed()`. The form adopts a new Creative in an `effect()`. The hub is disposed in `DestroyRef.onDestroy` |
| Separation of concerns | `core/` versus `creatives/*` | `CreativeSignalStore` holds screen state. Containers (`creative-list-page`, `creative-edit-page`) talk to it. `CreativesApi`, `CreativeHubService`, and `DisplayName` stay services. Presentational components (`creative-form`, `creative-list`, `presence-bar`, `conflict-banner`, `remote-update-notice`) use `input()` and `output()` only |
| Conflict and Rebase | `core/rebase.ts`, `creatives/conflict-banner` | Only fields the Collaborator changed are re-applied onto `current` |
| Live update versus Draft | `core/creative-signal-store.ts`, `creatives/remote-update-notice` | A dirty Draft is never overwritten |
| No logic in templates | `computed()` in each component | Templates bind to Signals only |

## Known limitations

- **Presence is single-instance.** It is in process memory with no backplane. ADR 0002 records why.
- **Display name uniqueness is by normalized form only.** Names are unique (case-insensitive, Unicode compatibility folded, unique index) and limited to letters, digits, spaces and `. _ - '`, 2 to 40 characters. Letters from different scripts that look alike (a Cyrillic `а` for a Latin `a`) are still different names, so a lookalike is possible. Accounts that existed before the rule keep their names, except duplicates, which got a short id suffix in the migration.
- **DNS rebinding is not fully mitigated.** The image check resolves the host once, and the HTTP client resolves it again on connect.
- **The `no redirects` setting is configuration**, not proven by a test: the stubbed handler cannot exercise it. Tests only prove a 3xx response is rejected.
- **Frontend and hub were tested against mocks.** The only end-to-end check done is a manual run of the HTTP contract against the running API.
