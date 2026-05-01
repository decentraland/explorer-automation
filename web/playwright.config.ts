import { defineConfig, devices } from '@playwright/test';

/**
 * Two projects:
 *   - `web`   → tests tagged @web   (browser-only, no desktop client involvement)
 *   - `cross` → tests tagged @cross (web → desktop handoff via auth-token-bridge.txt)
 *
 * The `cross` project runs serially (workers: 1) because the handoff manipulates
 * a single shared file at ~/Library/Application Support/DecentralandLauncherLight/
 * and only one Explorer instance can be running at a time.
 */
export default defineConfig({
  testDir: './tests',
  timeout: 120_000,
  expect: { timeout: 15_000 },
  retries: 1,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['allure-playwright', { outputFolder: 'allure-results' }],
  ],
  use: {
    baseURL: 'https://decentraland.org',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'web',
      grep: /@web/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'cross',
      grep: /@cross/,
      workers: 1,
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
