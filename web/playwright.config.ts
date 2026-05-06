import { defineConfig, devices } from '@playwright/test';

/**
 * Chrome launch args that enable WebGPU on the host's real GPU. Used by the
 * `webgpu` project below for the avatar-setup tests (Unity canvas + Wearable
 * Preview iframe). On macOS Chrome 147+ uses Metal natively for WebGPU; on
 * Linux runners with a Vulkan-capable GPU the same flags work.
 *
 * Note: deliberately NOT using SwiftShader / ANGLE software rendering. The
 * dapp's WebGL-detection rejects software-rendered contexts and routes to
 * the no-GPU `/auth/quick-setup` path instead of `/avatar-setup`. The browser
 * is launched headed (`headless: false` below) so the GPU process is real.
 */
const WEBGPU_ARGS = [
  '--enable-unsafe-webgpu',
  '--enable-features=Vulkan',
  '--ignore-gpu-blocklist',
];

/**
 * Three projects:
 *   - `web`    → tests tagged @web    (headless, no GPU)
 *   - `cross`  → tests tagged @cross  (web → desktop handoff via auth-token-bridge.txt)
 *   - `webgpu` → tests tagged @webgpu (Unity-rendered avatar editor; requires WebGPU)
 *
 * `cross` runs serially (workers: 1) because the handoff manipulates a single
 * shared file at ~/Library/Application Support/DecentralandLauncherLight/ and
 * only one Explorer instance can be running at a time.
 *
 * `webgpu` also runs serially and at a fixed 1200x997 viewport so the
 * relative-coordinate clicks in `AvatarSetupPage` land on the right grid cells.
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
      // `\b` so `@web` doesn't match `@webgpu` — Playwright's project grep
      // is a substring match by default.
      grep: /@web\b/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'cross',
      grep: /@cross/,
      workers: 1,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'webgpu',
      grep: /@webgpu/,
      workers: 1,
      // Unity-driven coordinate clicks are inherently flaky (GPU contention,
      // Unity warm-up race, no DOM signals to wait on). Retry up to twice
      // before declaring the test failed.
      retries: 2,
      timeout: 240_000,
      use: {
        ...devices['Desktop Chrome'],
        channel: 'chrome',
        headless: false,
        viewport: { width: 1200, height: 997 },
        launchOptions: { args: WEBGPU_ARGS },
      },
    },
  ],
});
