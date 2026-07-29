import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { uniqueUsername } from '../helpers/test-user.js'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import {
  setupMockedWallet,
  mockNoProfileOnCatalysts,
  installAutoWalletMockInitScript,
  applyPersonalSignOverride
} from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { createAuthRequest, pollAuthOutcome } from '../helpers/auth-server.js'
import { buildAuthChain } from '../../../shared/helpers/identity.js'

/**
 * The "RequestPage" flow — Decentraland's mechanism for letting a desktop
 * client (or any out-of-band signer) hand a wallet interaction off to a wallet
 * that lives in a browser session.
 *
 * The desktop side POSTs a request to the auth server (`createAuthRequest`)
 * and gets back a `requestId`. It then steers the user to
 * `decentraland.org/auth/requests/<id>`, where the user approves; the wallet
 * signs in-page; the desktop polls for the outcome (`pollAuthOutcome`).
 *
 * This file used to also cover `dcl_personal_sign` — the login handshake.
 * decentraland/auth#437 retired that method in auth-site 5.0.0 in favour of
 * the identity handoff (`?flow=deeplink` → `POST /identities` → the
 * `open?signin=<identityId>` deep link), so the sign-in test is gone. The
 * handoff itself is covered in the auth repo's own e2e suite
 * (`e2e/tests/explorer-metamask-flow.spec.ts`, `explorer-social-flow.spec.ts`)
 * against mocked services — not duplicated here. What an unmigrated client
 * still sending the retired method sees lives in
 * `request-page-failure.spec.ts`.
 */

const REDIRECT_TO = `${getBaseUrl()}/`

const { expect } = test

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
  await new LandingPage(page).waitForUrl()
  await unmockProfile()

  // 2. Build an auth chain (signer-grants-ephemeral) — required for every
  //    method the auth server still brokers. Then mint an
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

  // Injected wallets get the plain interaction view. The transaction-summary
  // variant — which swaps this control for `transfer-confirm-button` — is
  // gated on web2 (Magic / Thirdweb) wallets, which have no wallet UI of
  // their own to confirm in.
  const allowBtn = page.locator('[data-testid="wallet-interaction-allow-button"]')
  await allowBtn.waitFor({ state: 'visible', timeout: 30_000 })
  await allowBtn.click()

  const outcome = await pollAuthOutcome(requestId)
  expect(outcome.sender.toLowerCase()).toBe(account.address.toLowerCase())
  expect(outcome.result).toBe(MOCK_TX_HASH)
})
