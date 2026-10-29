import fs from 'node:fs';

/** Removes the throwaway database the API used for this run. */
export default function globalTeardown(): void {
  const db = process.env.E2E_DB_PATH;
  if (!db) return;
  for (const file of [db, `${db}-wal`, `${db}-shm`]) fs.rmSync(file, { force: true });
}
