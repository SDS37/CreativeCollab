# Frontend

The Angular app for CreativeCollab. How to run it, and how it fits the API, is in the [root README](../README.md). What each screen and service is is in [docs/architecture.md](../docs/architecture.md).

```bash
npm ci
npm start    # http://localhost:4200
npm test     # Vitest, HTTP and the hub mocked
```

There is no end-to-end target. `ng e2e` is not wired up.
