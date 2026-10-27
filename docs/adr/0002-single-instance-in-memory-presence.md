# Presence is single-instance and in-memory

Presence is held in memory in one server process and is lost on restart. We chose this over a persisted store or a scale-out backplane because Presence is short-lived and the app runs as one instance. Running more than one instance would split Presence (and live updates) between instances, so that change would need a backplane first.
