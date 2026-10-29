# Authenticated Collaborators with JWT

Collaborators sign in with an account, and the API trusts a signed JWT instead of a name the client typed. This replaces the v1 design, where the Display name was self-declared, unauthenticated, and kept in localStorage (frozen at git tag `v1-display-name`, archived in [docs/archive/v1](../archive/v1/architecture.md)). Nothing in ADR 0001 or 0002 changes.

- **Accounts:** self-hosted ASP.NET Core Identity on the existing SQLite context. Open registration with email and password. Roles are Viewer, Editor (the default) and Admin. The API enforces them with policies. Angular route guards only mirror them for UX.
- **Access token:** a JWT signed with HS256, valid 10 to 15 minutes, held in memory only and never in localStorage or sessionStorage. The key comes from user-secrets or an environment variable. Validation pins the algorithm and checks issuer, audience and lifetime. Claims are `sub`, `name`, `role` and `jti`.
- **Refresh token:** opaque, stored hashed, rotated on every use. Reusing an old one revokes the whole family. It travels in an `HttpOnly; Secure; SameSite=Strict` cookie scoped to the auth path. The app restores a session with one silent refresh on load. Logout is anonymous and works from the cookie alone, so an expired access token never stops a Collaborator from signing out.
- **Hosting:** the frontend and the API stay same-site. Unrelated domains would need `SameSite=None` and an anti-CSRF token, so that change needs a new ADR.
- **Identity on records:** `UpdatedBy` and Presence names come from token claims on the server. Older `UpdatedBy` values stay as plain text.
- **Account enumeration:** register answers a duplicate email with one generic message, and sign-in answers an unknown email and a wrong password alike. Registration is open without email verification, so a successful register versus a failed one still tells an attacker that an email is free. That cannot be closed without verification and is an accepted limit. Display names are public, so a taken name is reported as such.
- **Hub:** `CreativeHub` requires auth. The token arrives as `access_token` on `/hubs/*` only. A connection stays authorized until it drops, and reconnecting authenticates again.

We rejected an external identity provider (an outside dependency for a single-instance app), a hand-rolled user table (more security-sensitive code to own), and a token in localStorage (readable by any script on the page).
