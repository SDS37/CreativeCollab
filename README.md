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

It listens on `http://localhost:5114`. `dotnet run` with the default launch profile is already Development. Without Development there is no database and no seed.

**Angular app** (calls the API directly; CORS allows `http://localhost:4200`, configurable via `Cors:AllowedOrigins`):

```bash
cd frontend
npm ci
npm start      # http://localhost:4200
npm test       # service, store and component tests
```

To see a Conflict, open the same Creative in two browser windows under different Display names, save in one, then save in the other.

## Known limitations

Presence is single-instance, the Display name is trusted, and DNS rebinding is not fully mitigated. Details are in [REVIEW_NOTES.md](REVIEW_NOTES.md).
