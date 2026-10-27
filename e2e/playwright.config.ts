import path from 'node:path';
import { defineConfig, devices } from '@playwright/test';

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
      env: { ASPNETCORE_ENVIRONMENT: 'Development' },
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
