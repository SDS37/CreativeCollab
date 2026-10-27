# Definition of Done

The app is done when a colleague can run two browsers against one API and every box below is true. Boxes stay open until the software exists. Documentation alone does not check them.

## Must

- [x] `backend/CreativeCollab.Api` and `backend/CreativeCollab.Api.Tests` exist, and `dotnet test backend/CreativeCollab.slnx` passes
- [x] `GET /api/creatives` lists Creatives, most recently updated first
- [x] `GET /api/creatives/{id}` returns the Creative, or 404
- [x] `POST /api/creatives` returns 201, Version 1, and the Display name in `updatedBy`
- [x] Create rejects a missing required field, an over-long field, and an image URL that is not absolute http or https, as 400 `ValidationProblemDetails`
- [x] `PUT` (or the planned update route) requires the loaded Version and a Display name
- [x] A stale Version returns 409 with the latest Creative in `current`, including when the database reports a concurrency failure
- [x] Creatives survive a restart in `creativecollab.db`. Development applies migrations on startup. An empty table is seeded
- [x] `updatedAt` comes from an injected `TimeProvider`
- [x] Saving broadcasts `CreativeUpdated` to group `creative-{id}` on `/hubs/creatives`
- [x] Presence is the in-memory set of connections that have joined that Creative, and a dropped connection emits `UserLeft`
- [x] An image URL that changes is checked with a short HEAD (then a ranged GET). A non-image, a private address, or a network failure is 400
- [x] The Angular app asks for and remembers a Display name, then lists, opens, creates, and edits a Creative, with explicit loading and error states. Unit tests mock HTTP. Playwright runs the same path against the API
- [x] A Conflict leaves the Draft on screen and offers Load latest and Keep my edits. Unit tests mock the 409. Playwright resolves one against the API
- [x] A `CreativeUpdated` event does not overwrite a dirty Draft. A clean form updates silently, and the edit page shows Presence. Unit tests use a fake hub. Playwright checks Presence, the dirty Draft, and leave against the real hub
- [x] The root README is a runbook for the API and the Angular app

## Not in this definition

Accounts, a unique Name, delete, a Version history, a merge UI, more than one API instance, and a SignalR backplane. Presence across instances would need a new decision: [ADR 0002](adr/0002-single-instance-in-memory-presence.md).
