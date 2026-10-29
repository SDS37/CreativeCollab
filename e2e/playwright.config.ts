import { randomBytes } from 'node:crypto';
import path from 'node:path';
import { defineConfig, devices } from '@playwright/test';
import { ADMIN } from './tests/accounts';

const root = path.resolve(__dirname, '..');

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
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
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      // The API refuses to start without a signing key. A throwaway one per run keeps it out of the repository.
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        Jwt__Key: process.env.Jwt__Key ?? randomBytes(48).toString('base64'),
        // The seeded Admin is how the suite makes a Viewer. Test-only values for the local development database.
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
