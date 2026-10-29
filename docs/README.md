# CreativeCollab documentation

Several Collaborators edit the same Creative at once. A save with a stale Version is a Conflict. The Collaborator loads the latest Version or rebases their Draft. Words in that sentence are defined in [GLOSSARY.md](../GLOSSARY.md).

Read in this order if you are new. Open one file if you are about to change that area.

| Document | What it decides |
|---|---|
| [Architecture](architecture.md) | What runs today, and what each part is |
| [Definition of Done](DoD.md) | When the app is complete, and which boxes the code already checks |
| [Evolution](evolution.md) | v1 (Display name) vs v2 (JWT accounts), side by side |
| [Plan](../PLAN.md) | The agreed build order and the rules for each step |
| [Architecture decision records](architecture-decision-records.md) | Choices already made, with the files that hold them |
| [Commits](commits.md) | How a change is committed |
| [Glossary](../GLOSSARY.md) | The words the project uses, and the words it avoids |

Agent conventions (issue tracker, triage labels, how to read the glossary) live in [docs/agents](agents/domain.md).

## Status

The plan is in the tree. The API lists, gets, creates, and updates Creatives, rejects a stale Version with 409, stores them in SQLite, broadcasts a save, and tracks Presence. The Angular app lists, creates, and edits, and it keeps a Draft across a Conflict and a live update. What is deliberately left out is in the [Definition of Done](DoD.md). The picture of the running system is the [architecture](architecture.md).
