> **Superseded.** These are the v1 glossary entries for people, frozen at git tag `v1-display-name`. See [ADR 0003](../../adr/0003-authenticated-collaborators-with-jwt.md) and [evolution](../../evolution.md).

**Collaborator**:
Anyone using the app, identified only by a Display name. Viewers count as Collaborators, not just those typing.
_Avoid_: User, editor

**Display name**:
The self-declared, unauthenticated name a Collaborator gives. It is trusted and not unique. It is recorded on a Creative as who last updated it, and it is empty only for seeded Creatives.
_Avoid_: Username, login
