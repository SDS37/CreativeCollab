# Architecture decision records

Decisions that are hard to reverse live in [docs/adr](adr/). Each file is the decision. This page is the index. Change a decision by adding a new ADR that says which one it replaces. Do not silently rewrite the old file.

The longer design notes that are not yet a single decision (image checks, the Angular Conflict screen, SQLite) stay in [PLAN.md](../PLAN.md) until they earn their own ADR.

| ID | Title | Status |
|---|---|---|
| [0001](adr/0001-optimistic-concurrency-with-integer-version.md) | Optimistic concurrency with an integer Version | Accepted |
| [0002](adr/0002-single-instance-in-memory-presence.md) | Presence is single-instance and in-memory | Accepted |
| [0003](adr/0003-authenticated-collaborators-with-jwt.md) | Authenticated Collaborators with JWT | Accepted, being implemented |
| [0004](adr/0004-stay-on-minimal-apis.md) | Stay on minimal APIs | Accepted |
| [0005](adr/0005-experimental-playwright-component-testing.md) | Experimental Playwright component testing | Accepted, being implemented |

## How to read them

ADR 0001 is why a save carries a Version instead of a lock or a merge. The Conflict UX (Load latest, Keep my edits) follows from that choice. The 409 body and the EF concurrency token are that decision in the code, described in the [architecture](architecture.md).

ADR 0002 is why Presence is a dictionary in one process. Live updates share that limit: a second instance would split both, until a backplane exists.

ADR 0003 replaces the v1 Display name design, which was not an ADR; [evolution](evolution.md) compares the two.
