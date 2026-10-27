# Definition of Done

The app is done when a colleague can run two browsers against one API and every box below is true. Boxes stay open until the software exists. Documentation alone does not check them.

## Must

- [x] `backend/CreativeCollab.Api` and `backend/CreativeCollab.Api.Tests` exist, and `dotnet test backend/CreativeCollab.slnx` passes
- [x] `GET /api/creatives` lists Creatives, most recently updated first
- [x] `GET /api/creatives/{id}` returns the Creative, or 404
- [x] `POST /api/creatives` returns 201, Version 1, and the Display name in `updatedBy`
- [x] Create rejects a missing required field, an over-long field, and an image URL that is not absolute http or https, as 400 `ValidationProblemDetails`
- [ ] `PUT` (or the planned update route) requires the loaded Version and a Display name
- [ ] A stale Version returns 409 with the latest Creative in `current`, including when the database reports a concurrency failure
- [ ] Creatives survive a restart in `creativecollab.db`. Development applies migrations on startup. An empty table is seeded
- [ ] `updatedAt` comes from an injected `TimeProvider`
- [ ] Saving broadcasts `CreativeUpdated` to group `creative-{id}` on `/hubs/creatives`
- [ ] Presence is the in-memory set of connections that have joined that Creative, and a dropped connection emits `UserLeft`
- [ ] An image URL that changes is checked with a short HEAD (then a ranged GET). A non-image, a private address, or a network failure is 400
- [x] The Angular app asks for and remembers a Display name, then lists, opens, creates, and edits a Creative, with explicit loading and error states (tested against mocked HTTP; not yet run against the real API)
- [ ] A Conflict leaves the Draft on screen and offers Load latest and Keep my edits
- [ ] A `CreativeUpdated` event does not overwrite a dirty Draft
- [ ] The root README is a runbook for the API and the Angular app

## Not in this definition

Accounts, a unique Name, delete, a Version history, a merge UI, more than one API instance, and a SignalR backplane. Presence across instances would need a new decision: [ADR 0002](adr/0002-single-instance-in-memory-presence.md).
