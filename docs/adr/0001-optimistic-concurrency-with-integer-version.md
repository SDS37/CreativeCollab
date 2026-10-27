# Optimistic concurrency with an integer Version

Concurrent edits are guarded by an integer Version that the client sends back on save. A stale Version is rejected as a Conflict, and the Collaborator resolves it by loading the latest or rebasing their Draft. We rejected pessimistic locking (a held lock blocks teammates and needs release rules when someone walks away) and real-time merge (CRDT/OT), which is far more machinery than a four-field Creative needs. Conflicts are expected to be rare and cheap to resolve by hand.
