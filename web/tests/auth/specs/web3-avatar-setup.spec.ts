import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import {
  setupMockedWallet,
  mockNoProfileOnCatalysts,
  stubNavigatorGpu,
  installAutoWalletMockInitScript,
  applyPersonalSignOverride
} from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { AuthPage } from '../pages/AuthPage.js'
import { AvatarSetupPage } from '../pages/AvatarSetupPage.js'

/**
 * Drives the WebGL avatar editor at `/auth/avatar-setup` (Unity canvas inside
 * the WearablePreview iframe).
 *
 * Background: the dapp's POST-LOGIN routing always sends users to
 * `/auth/quick-setup` now (see `useEnsureProfile` + `useSetupNavigation` in
 * `decentraland/auth`). The `/avatar-setup` route is deprecated as an
 * automatic destination but is still **directly addressable** — its
 * component lives at `<Route path="/avatar-setup" />` and the only guard is
 * an internal WebGPU check that bounces to `/setup` when
 * `navigator.gpu.requestAdapter()` returns null.
 *
 * Strategy here:
 *   1. `stubNavigatorGpu` returns a non-null adapter so the guard passes
 *      regardless of host GPU state (headed Chrome on macOS often returns
 *      null even with real hardware).
 *   2. Run the standard web3 login. Land on `/auth/quick-setup`.
 *   3. Navigate directly to `/auth/avatar-setup` and drive the editor.
 *
 * Tagged `@webgpu` and excluded from `npm test`. Run with `npm run test:webgpu`.
 *
 * Mirrors `auth-e2e-tests`' `web3-mocked-wallet-new-login-avatar-setup.spec.ts`
 * in spirit; the direct-nav + stub approach is needed because their pre-login
 * auto-routing assumption no longer holds against current prod.
 */

const REDIRECT_TO = `${getBaseUrl()}/`
const AVATAR_SETUP_URL = `${getBaseUrl()}/auth/avatar-setup`
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`

const { expect } = test

test('@webgpu new user web3 + full avatar customization', async ({ page, ethereumWalletMock }) => {
  const privateKey = generatePrivateKey()
  const account = privateKeyToAccount(privateKey)

  await stubNavigatorGpu(page)
  await mockNoProfileOnCatalysts(page)
  await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
  await new AuthPage(page).clickMetaMaskButton()

  // Direct navigation to /auth/avatar-setup. Pre-install Web3Mock auto-remock
  // so the page sees our wallet address immediately; without it, the page's
  // own auth check sees `account = null` and bounces to /auth/login.
  await installAutoWalletMockInitScript(page, account.address)
  await page.goto(AVATAR_SETUP_URL, { waitUntil: 'load' })
  await applyPersonalSignOverride(page)

  const avatar = new AvatarSetupPage(page)
  await avatar.waitFor()
  await avatar.completeProfile(uniqueUsername(), `${uniqueUsername()}@example.com`)
  await avatar.completeAvatarCustomization()

  await page.waitForURL(/\/download/, { timeout: 30_000 })
  expect(page.url()).toMatch(/\/download/)
})

test('@webgpu new user web3 + skip avatar customization', async ({ page, ethereumWalletMock }) => {
  const privateKey = generatePrivateKey()
  const account = privateKeyToAccount(privateKey)

  await stubNavigatorGpu(page)
  await mockNoProfileOnCatalysts(page)
  await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
  await new AuthPage(page).clickMetaMaskButton()

  await installAutoWalletMockInitScript(page, account.address)
  await page.goto(AVATAR_SETUP_URL, { waitUntil: 'load' })
  await applyPersonalSignOverride(page)

  const avatar = new AvatarSetupPage(page)
  await avatar.waitFor()
  await avatar.completeProfile(uniqueUsername(), `${uniqueUsername()}@example.com`)
  await avatar.skipAvatarCustomization()

  await page.waitForURL(/\/download/, { timeout: 30_000 })
  expect(page.url()).toMatch(/\/download/)
})
