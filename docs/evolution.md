# Evolution: v1 and v2

How the app's identity and auth changed. v1 is frozen at git tag `v1-display-name` and archived in [docs/archive/v1](archive/v1/architecture.md). v2 is decided in [ADR 0003](adr/0003-authenticated-collaborators-with-jwt.md), and the code lands slice by slice (see the GitHub issues labelled `ready-for-agent`).

| | v1: Display name | v2: JWT accounts |
|---|---|---|
| Who is a Collaborator | Anyone who types a name | A registered account with a role |
| Identity source | The client sends the name | The server reads token claims |
| Where it is kept | localStorage | Access token in memory, refresh token in an httpOnly cookie |
| Spoofing | Any client can write any `UpdatedBy` | `UpdatedBy` and Presence come from the token, and a Display name is unique and plain, so it cannot imitate another |
| API access | Open | Login required, except register, login and refresh |
| Roles | None | Viewer, Editor, Admin, enforced by API policies |
| Hub | Open, name passed to `JoinCreative` | `[Authorize]`, token as `access_token` on `/hubs/*` |
| Backend style | Minimal APIs | Minimal APIs ([ADR 0004](adr/0004-stay-on-minimal-apis.md)) |
| Frontend identity | `DisplayName` service and prompt | Session store, interceptor, `authGuard` and `roleGuard` |
| Component tests | Vitest and jsdom | Playwright component tests ([ADR 0005](adr/0005-experimental-playwright-component-testing.md)), logic stays on Vitest |
| Main risk | Trust: nothing proves who saved | Token handling: signing key, refresh reuse, same-site hosting |

## Reading order

1. [v1 architecture](archive/v1/architecture.md) and [v1 glossary entries](archive/v1/glossary-people.md).
2. [ADR 0003](adr/0003-authenticated-collaborators-with-jwt.md), which gives the reasons for the change.
3. The live [architecture](architecture.md) and [glossary](../GLOSSARY.md), which are updated as each v2 slice merges.

The transition is complete: the API, the Angular app and the end-to-end suite all run on v2. The live connection renews its token before every connect and reconnect, and stops when renewal fails. The Playwright suite registers two real accounts with unique emails and covers two Editors on one Creative, a Conflict that names the other account, Presence showing both account names, and a Viewer (role set through the Admin endpoint by the seeded Admin) who cannot create or edit but still sees live updates and Presence.
