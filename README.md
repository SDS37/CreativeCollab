# CreativeCollab

A small workspace where several people edit the same ad Creative at once. A save that lost the race is rejected, and the editor keeps their draft.

The API is in `backend/` (.NET 10, EF Core and SQLite, SignalR). The Angular app is in `frontend/`. Start with [docs/README.md](docs/README.md). Reviewers: see [REVIEW_NOTES.md](REVIEW_NOTES.md).

## Run it

Requires the .NET 10 SDK and Node 24.

**API** (Development applies migrations and seeds three Creatives into `creativecollab.db`):

```bash
dotnet test backend/CreativeCollab.slnx
ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/CreativeCollab.Api
```

It listens on `http://localhost:5114`. That address has no page: the list of Creatives is `http://localhost:5114/api/creatives`. The app to open in a browser is `http://localhost:4200/`. `dotnet run` with the default launch profile is already Development. Without Development there is no database and no seed. Seed runs only when the Creatives table is empty.

**Angular app** (calls the API directly; CORS allows `http://localhost:4200`, configurable via `Cors:AllowedOrigins`):

```bash
cd frontend
npm ci
npm start      # http://localhost:4200
npm test       # signal store, API, hub, and component tests
```

To see a Conflict, open the same Creative in two browser windows under different Display names, save in one, then save in the other.

**Playwright** (starts the API and the Angular app itself when they are not already running):

```bash
cd e2e
npm ci
npx playwright install chromium
npm test
```

CI runs the backend tests, the Angular tests, and Playwright on every push to `main` and every pull request.

## Known limitations

Presence is single-instance, the Display name is trusted, and DNS rebinding is not fully mitigated. Details are in [REVIEW_NOTES.md](REVIEW_NOTES.md).
