import angular from '@analogjs/vite-plugin-angular';
import { defineConfig, devices } from '@sand4rt/experimental-ct-angular';

export default defineConfig({
  testDir: './src',
  testMatch: '**/*.ct.ts',
  snapshotDir: './playwright/__snapshots__',
  forbidOnly: !!process.env['CI'],
  fullyParallel: true,
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? 'github' : 'list',
  use: {
    trace: 'on-first-retry',
    ctPort: 3100,
    // The Angular compiler plugin registers signal inputs and outputs, which the default esbuild transform does not.
    ctViteConfig: {
      plugins: [angular({ jit: true, tsconfig: './tsconfig.ct.json' })],
    },
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
