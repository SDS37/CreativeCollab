# CreativeCollab documentation

Several Collaborators edit the same Creative at once. A save with a stale Version is a Conflict. The Collaborator loads the latest Version or rebases their Draft. Words in that sentence are defined in [GLOSSARY.md](../GLOSSARY.md).

Read in this order if you are new. Open one file if you are about to change that area.

| Document | What it decides |
|---|---|
| [Architecture](architecture.md) | What runs today, and the shape the rest of the build is aiming at |
| [Definition of Done](DoD.md) | When the app is complete, and which boxes the code already checks |
| [Plan](../PLAN.md) | The agreed build order and the rules for each step |
| [Architecture decision records](architecture-decision-records.md) | Choices already made, with the files that hold them |
| [Commits](commits.md) | How a change is committed |
| [Glossary](../GLOSSARY.md) | The words the project uses, and the words it avoids |

Agent conventions (issue tracker, triage labels, how to read the glossary) live in [docs/agents](agents/domain.md).

## Status

The API walking skeleton is in `backend/`. `GET` and `POST /api/creatives` work. Creatives are stored in memory. Create is validated with FluentValidation. A new Creative starts at Version 1 and records the Display name.

Update, Conflict (409), SQLite, SignalR, Presence, the image check, and the Angular app are specified in the [plan](../PLAN.md) and are not in the tree yet.
