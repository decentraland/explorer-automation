import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import {
  setupMockedWallet,
  mockNoProfileOnCatalysts,
  installAutoWalletMockInitScript,
  applyPersonalSignOverride
} from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { HomePage } from '../pages/HomePage.js'
import { createAuthRequest, pollAuthOutcome } from '../helpers/auth-server.js'
import { buildAuthChain, getEphemeralMessage } from '../../../shared/helpers/identity.js'

/**
 * The "RequestPage" flow — Decentraland's mechanism for letting a desktop
 * client (or any out-of-band signer) hand off signature requests to a wallet
 * that lives in a browser session.
 *
 * The desktop side POSTs a request to the auth server (`createAuthRequest`)
 * and gets back a `requestId`. It then steers the user to
 * `decentraland.org/auth/requests/<id>`, where the user approves; the wallet
 * signs in-page; the desktop polls for the outcome (`pollAuthOutcome`).
 *
 * Two specs:
 *   - `dcl_personal_sign` — the login handshake (sign an ephemeral message).
 *   - `eth_sendTransaction` — a wallet interaction (return a tx hash).
 *
 * Mirrors `auth-e2e-tests`' `web3-request-page-{login,wallet-interaction}.spec.ts`.
 * Both tests need an established DCL profile (they self-bootstrap via web3
 * signup) before driving the RequestPage.
 */

const REDIRECT_TO = `${getBaseUrl()}/`
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`

const { expect } = test

test('@web @auth RequestPage login (dcl_personal_sign)', async ({ page, ethereumWalletMock }) => {
  const privateKey = generatePrivateKey()
  const account = privateKeyToAccount(privateKey)

  // 1. Register a DCL profile for our wallet.
  const unmockProfile = await mockNoProfileOnCatalysts(page)
  await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
  await new AuthPage(page).clickMetaMaskButton()
  const qs = new QuickSetupPage(page)
  await qs.waitFor()
  await qs.fillUsername(uniqueUsername())
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()
  await new HomePage(page).waitFor()
  await unmockProfile()

  // 2. Simulate the desktop side: generate an ephemeral key pair, ask the
  //    auth server to mint a `dcl_personal_sign` request for the ephemeral
  //    message that grants signing rights to the ephemeral key.
  const ephemeralPrivateKey = generatePrivateKey()
  const ephemeralAccount = privateKeyToAccount(ephemeralPrivateKey)
  const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
  const ephemeralMessage = getEphemeralMessage(ephemeralAccount.address, expiration)
  const { requestId } = await createAuthRequest('dcl_personal_sign', [ephemeralMessage])
  expect(requestId).toBeTruthy()

  // 3. Drive the user-facing RequestPage. The RequestPage probes `window.ethereum`
  //    immediately on page load to decide whether to render the approval UI or
  //    bounce to /auth/login — so we install an init script that auto-remocks
  //    Web3Mock as soon as it appears on the new navigation, before any
  //    rebindWalletMock call could catch it.
  // Auto-remock Web3Mock on this navigation, BEFORE goto, so the dapp sees
  // our address from page-load time. Skip the synpress connectToDapp/import
  // calls — they re-introduce the mock's default address mid-handshake on
  // prod's RequestPage, which crashes the signing flow with an "unknown RPC
  // error". A simple personal_sign re-override is enough.
  await installAutoWalletMockInitScript(page, account.address)
  await page.goto(`/auth/requests/${requestId}`, { waitUntil: 'load' })
  await applyPersonalSignOverride(page)
  const approveBtn = page.locator('[data-testid="verify-sign-in-approve-button"]')
  await approveBtn.waitFor({ state: 'visible', timeout: 30_000 })
  await approveBtn.click()

  // 4. Poll the auth server for the signed outcome.
  const outcome = await pollAuthOutcome(requestId)
  expect(outcome.sender.toLowerCase()).toBe(account.address.toLowerCase())
  // EIP-191 personal_sign signatures are 65 bytes → 132 hex chars + "0x".
  expect(outcome.result).toMatch(/^0x[0-9a-f]{130}$/i)
})

test('@web @auth RequestPage wallet interaction (eth_sendTransaction)', async ({ page, ethereumWalletMock }) => {
  const privateKey = generatePrivateKey()
  const account = privateKeyToAccount(privateKey)
  // Mocked tx hash returned by the wallet for the eth_sendTransaction stub.
  const MOCK_TX_HASH = `0x${'ab'.repeat(32)}`

  // 1. Register a DCL profile for our wallet.
  const unmockProfile = await mockNoProfileOnCatalysts(page)
  await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
  await new AuthPage(page).clickMetaMaskButton()
  const qs = new QuickSetupPage(page)
  await qs.waitFor()
  await qs.fillUsername(uniqueUsername())
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()
  await new HomePage(page).waitFor()
  await unmockProfile()

  // 2. Build an auth chain (signer-grants-ephemeral) — required for any
  //    non-`dcl_personal_sign` method on the auth server. Then mint an
  //    `eth_sendTransaction` request.
  const ephemeralPrivateKey = generatePrivateKey()
  const ephemeralAccount = privateKeyToAccount(ephemeralPrivateKey)
  const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
  const authChain = await buildAuthChain(privateKey, ephemeralAccount.address, expiration)

  const recipient = privateKeyToAccount(generatePrivateKey()).address
  const txParams = { from: account.address, to: recipient, value: '0x0', data: '0x' }
  const { requestId } = await createAuthRequest('eth_sendTransaction', [txParams], authChain)
  expect(requestId).toBeTruthy()

  // 3. Drive the RequestPage. After re-binding the wallet, also patch the
  //    `eth_sendTransaction` handler to return our mocked tx hash — the
  //    real Ethereum RPC isn't reachable from the mock and we just want to
  //    validate the auth-server handshake.
  // Auto-remock Web3Mock on this navigation, BEFORE goto, so the dapp sees
  // our address from page-load time. Skip the synpress connectToDapp/import
  // calls — they re-introduce the mock's default address mid-handshake on
  // prod's RequestPage, which crashes the signing flow with an "unknown RPC
  // error". A simple personal_sign re-override is enough.
  await installAutoWalletMockInitScript(page, account.address)
  await page.goto(`/auth/requests/${requestId}`, { waitUntil: 'load' })
  await applyPersonalSignOverride(page)
  await page.evaluate(mockTxHash => {
    type Eth = { request: (a: { method: string; params?: unknown[] }) => Promise<unknown> }
    const w = window as unknown as { ethereum: Eth }
    const original = w.ethereum.request.bind(w.ethereum)
    w.ethereum.request = async args => {
      if (args.method === 'eth_sendTransaction') return mockTxHash
      return original(args)
    }
  }, MOCK_TX_HASH)

  const allowBtn = page.locator('[data-testid="wallet-interaction-allow-button"]')
  await allowBtn.waitFor({ state: 'visible', timeout: 30_000 })
  await allowBtn.click()

  const outcome = await pollAuthOutcome(requestId)
  expect(outcome.sender.toLowerCase()).toBe(account.address.toLowerCase())
  expect(outcome.result).toBe(MOCK_TX_HASH)
})
