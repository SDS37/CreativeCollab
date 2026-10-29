# CreativeCollab

A small collaborative workspace where several people edit the same ad Creative at once without silently overwriting each other's work.

## Language

**Creative**:
The whole editable record of one ad: its Name, Headline, body, optional image and call-to-action. Creatives have no lifecycle status (no draft/published).
_Avoid_: Ad, asset, item

**Name**:
The internal label used to find a Creative in the list. Never shown to the ad's audience, and not unique.
_Avoid_: Title, label

**Headline**:
The audience-facing headline copy of a Creative.
_Avoid_: Title

### Editing and concurrency

**Draft**:
A Collaborator's unsaved local edits to a Creative. A Draft is never shared until saved.
_Avoid_: Pending changes, working copy

**Version**:
The integer counter of a Creative that rises by one on every successful save. It is not a history: earlier Versions are not kept.
_Avoid_: Revision, history, snapshot

**Conflict**:
A save rejected because the Creative's Version has moved on since the Collaborator last loaded it.
_Avoid_: Collision, clash, stale save

**Rebase**:
Re-applying only the fields a Collaborator actually changed in their Draft onto the latest Version of the Creative, then saving again. Fields they did not change take the latest values.
_Avoid_: Overwrite, force save, merge

### People

**Collaborator**:
Anyone using the app, identified only by a Display name. Viewers count as Collaborators, not just those typing.
_Avoid_: User, editor

**Account**:
A registered identity with an email, a password, a Display name and one role (Viewer, Editor or Admin). The API signs an Account in and issues its tokens. Registration gives the Editor role.
_Avoid_: Profile, login

**Display name**:
The self-declared, unauthenticated name a Collaborator gives. It is trusted and not unique. It is recorded on a Creative as who last updated it, and it is empty only for seeded Creatives.
_Avoid_: Username, login

**Presence**:
The set of Collaborators who currently have a given Creative open. It counts each open session separately, so one person in two tabs appears twice. It is not persisted.
_Avoid_: Online status, activity
