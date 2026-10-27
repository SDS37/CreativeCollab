# Commits

Messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/).

```
type(scope): short description
```

The description is the reason, in the imperative, and it fits on one line. Use the body when the why is not obvious from that line.

Commits already on `main` before this file used a plain imperative sentence (`Add API walking skeleton…`). Leave them. New commits use the form below.

## Types

| Type | When |
|---|---|
| feat | A new slice of the plan (an endpoint, the hub, a screen) |
| fix | A bug fix |
| docs | Documentation only |
| refactor | A change that neither fixes a bug nor adds a feature |
| test | Tests only |
| build | Build system or dependencies |
| ci | CI configuration |
| chore | Tooling and other housekeeping |

## Scopes

Use the area the change lives in.

| Scope | Area |
|---|---|
| api | `backend/CreativeCollab.Api` |
| tests | `backend/CreativeCollab.Api.Tests` |
| web | Angular app, once `frontend/` exists |
| docs | Documentation |
| mcp | `.mcp.json` and `.cursor/mcp.json` |

Omit the scope when the change is the whole repo (`docs: describe how a Conflict is stored`).

## Examples

```
feat(api): reject a stale Version with the latest Creative
feat(web): keep a dirty Draft when CreativeUpdated arrives
fix(api): treat an empty image URL as absent
docs: record that Presence is single-instance
test(api): cover a 409 whose current body matches the store
```

## What a commit contains

- One step from the [plan](../PLAN.md). A commit that adds the hub and also skins the edit page is two commits.
- The test that shows the new behaviour, in the same commit as the behaviour, unless the change cannot be tested yet.
- No `.env` files, tokens, or connection strings. `GITHUB_PERSONAL_ACCESS_TOKEN` stays an environment reference, never a value.
