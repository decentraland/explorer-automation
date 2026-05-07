import { defineConfig, devices } from '@playwright/test'
import { config as loadDotenv } from 'dotenv'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

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
const WEBGPU_ARGS = ['--enable-unsafe-webgpu', '--enable-features=Vulkan', '--ignore-gpu-blocklist']

// Load .env so MARKETPLACE_BASE_URL etc. are available when this module evaluates.
const __dirname = path.dirname(fileURLToPath(import.meta.url))
loadDotenv({ path: path.resolve(__dirname, '../.env') })

// .org is publicly reachable; .zone is gated behind Cloudflare Access (internal-only).
// Use ?env=dev (via `withEnv()` in shared/helpers/url.ts) to switch the dapp to
// Polygon Amoy / Sepolia while still hitting the public .org host.
const BASE_URL = (process.env.BASE_URL ?? 'https://decentraland.org').replace(/\/$/, '')
// Trailing slash is required so relative `goto('browse')` resolves under /marketplace/.
// Without it, `goto('/browse')` would replace the path and hit the root landing page.
const MARKETPLACE_BASE_URL = process.env.MARKETPLACE_BASE_URL ?? `${BASE_URL}/marketplace/`

/**
 * Five projects:
 *   - `web`                  → tests tagged @web    (headless, no GPU)
 *   - `cross`                → tests tagged @cross  (web → desktop handoff via auth-token-bridge.txt)
 *   - `webgpu`               → tests tagged @webgpu (Unity-rendered avatar editor; requires WebGPU)
 *   - `marketplace`          → marketplace off-chain specs (@marketplace, excluding @on-chain)
 *   - `marketplace-onchain`  → marketplace on-chain specs (@on-chain) — needs funded wallets
 *
 * `cross` runs serially (workers: 1) because the handoff manipulates a single
 * shared file at ~/Library/Application Support/DecentralandLauncherLight/ and
 * only one Explorer instance can be running at a time.
 *
 * `webgpu` also runs serially and at a fixed 1200x997 viewport so the
 * relative-coordinate clicks in `AvatarSetupPage` land on the right grid cells.
 */
export default defineConfig({
  // testDir is set per-project below; auth specs live under tests/auth/specs,
  // marketplace under tests/marketplace/specs.
  timeout: 120_000,
  expect: { timeout: 15_000 },
  forbidOnly: !!process.env.CI,
  retries: 1,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['allure-playwright', { outputFolder: 'allure-results' }]
  ],
  use: {
    baseURL: 'https://decentraland.org',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    {
      name: 'web',
      testDir: './tests/auth/specs',
      // `\b` so `@web` doesn't match `@webgpu` — Playwright's project grep
      // is a substring match by default.
      grep: /@web\b/,
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'cross',
      testDir: './tests/auth/specs',
      grep: /@cross/,
      workers: 1,
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'webgpu',
      testDir: './tests/auth/specs',
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
        launchOptions: { args: WEBGPU_ARGS }
      }
    },
    {
      name: 'marketplace',
      testDir: './tests/marketplace/specs',
      grep: /@marketplace/,
      grepInvert: /@on-chain/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: MARKETPLACE_BASE_URL
      }
    },
    {
      // On-chain specs share a 2-wallet pool. Cross-file serial is enforced
      // by `--workers=1` in the npm script (test:marketplace:onchain);
      // `fullyParallel: false` here is belt-and-suspenders for direct invocations.
      // `retries: 0` because retrying a partially-broadcast tx hits
      // max-per-wallet on attempt 2 — fail loudly instead.
      name: 'marketplace-onchain',
      testDir: './tests/marketplace/specs',
      grep: /@on-chain/,
      fullyParallel: false,
      retries: 0,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: MARKETPLACE_BASE_URL
      }
    }
  ]
})
