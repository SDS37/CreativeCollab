# CreativeCollab

A small workspace where several people edit the same ad Creative at once. A save that lost the race is rejected, and the editor keeps their draft.

The walking skeleton is the API in `backend/`: list, get, and create. Update, live presence, and the Angular app are still to build. Start with [docs/README.md](docs/README.md).

## Run the API

Requires the .NET 10 SDK.

```bash
dotnet test backend/CreativeCollab.slnx
dotnet run --project backend/CreativeCollab.Api
```

The API listens on `http://localhost:5114`. Creatives are stored in memory and disappear when the process stops.
