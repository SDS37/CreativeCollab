import { randomBytes } from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import { defineConfig, devices } from '@playwright/test';
import { ADMIN } from './tests/accounts';

const root = path.resolve(__dirname, '..');

// Generated once per run. Playwright loads this file again in every worker, so a value already in the
// environment (set by the main process, which workers inherit) is kept and nothing is committed.
process.env.E2E_ADMIN_PASSWORD ??= randomBytes(24).toString('base64url');
// The API runs on its own throwaway SQLite file, never the developer's creativecollab.db.
process.env.E2E_DB_PATH ??= path.join(os.tmpdir(), `creativecollab-e2e-${randomBytes(6).toString('hex')}.db`);

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  globalTeardown: './global-teardown.ts',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command:
        'dotnet run --project backend/CreativeCollab.Api --no-launch-profile --urls http://localhost:5114',
      cwd: root,
      url: 'http://localhost:5114/api/creatives',
      // Never reuse a running API: it would have another Admin password and another database.
      reuseExistingServer: false,
      timeout: 180_000,
      // The API refuses to start without a signing key. A throwaway one per run keeps it out of the repository.
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        Jwt__Key: process.env.Jwt__Key ?? randomBytes(48).toString('base64'),
        ConnectionStrings__Creatives: `Data Source=${process.env.E2E_DB_PATH};Pooling=False`,
        // The seeded Admin is how the suite makes a Viewer, in the throwaway database above.
        Admin__Email: ADMIN.email,
        Admin__Password: ADMIN.password,
        Admin__DisplayName: ADMIN.displayName,
      },
    },
    {
      command: 'npm start -- --host localhost --port 4200',
      cwd: path.join(root, 'frontend'),
      url: 'http://localhost:4200',
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
    },
  ],
});
