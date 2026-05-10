import { generatePrivateKey } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, mockNoProfileOnCatalysts } from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'

/**
 * New-user signup flows. All variants use a mocked web3 wallet with a fresh
 * private key per test, so they don't consume Thirdweb OTP quota and can run
 * unlimited times. Three variants mirror the previous OTP suite and add a
 * dedicated avatar-customization test.
 *
 * Two pieces of plumbing are required for fresh wallets to actually reach
 * `/auth/quick-setup` on prod:
 *   1. `mockNoProfileOnCatalysts` — forces 404 on the catalyst profile lookup
 *      so `useEnsureProfile` treats us as new.
 *   2. `redirectTo` — disables the dapp's `useSkipSetup` short-circuit (which
 *      sends users straight home when `ONBOARDING_TO_EXPLORER` is on AND no
 *      external redirectTo is present).
 */

const { expect } = test
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`
const REDIRECT_TO = `${getBaseUrl()}/`

test.describe('@web @auth new user signup (web3)', () => {
  test('new user can sign up without subscribing to newsletter', async ({ page, ethereumWalletMock }) => {
    await mockNoProfileOnCatalysts(page)
    await setupMockedWallet(page, ethereumWalletMock, {
      privateKey: generatePrivateKey(),
      redirectTo: REDIRECT_TO
    })
    const auth = new AuthPage(page)
    const qs = new QuickSetupPage(page)
    const landing = new LandingPage(page)

    await auth.clickMetaMaskButton()

    await qs.waitFor()
    await qs.fillUsername(uniqueUsername())
    // intentionally NOT calling subscribeToNewsletter — leaves the email field blank.
    await qs.acceptTerms()
    await qs.submit()

    await qs.clickStartExploring()
    await landing.waitForUrl()
    expect(page.url()).not.toMatch(/\/auth/)
  })

  test('new user can sign up and subscribe to newsletter', async ({ page, ethereumWalletMock }) => {
    await mockNoProfileOnCatalysts(page)
    await setupMockedWallet(page, ethereumWalletMock, {
      privateKey: generatePrivateKey(),
      redirectTo: REDIRECT_TO
    })
    const auth = new AuthPage(page)
    const qs = new QuickSetupPage(page)
    const landing = new LandingPage(page)

    await auth.clickMetaMaskButton()

    await qs.waitFor()
    const username = uniqueUsername()
    await qs.fillUsername(username)
    // Newsletter opt-in. Wallet auth doesn't need an email; this is purely
    // for the dapp's optional newsletter subscription.
    await qs.subscribeToNewsletter(`${username}@example.com`)
    await qs.acceptTerms()
    await qs.submit()

    await qs.clickStartExploring()
    await landing.waitForUrl()
    expect(page.url()).not.toMatch(/\/auth/)
  })

  test('new user can customize avatar during signup', async ({ page, ethereumWalletMock }) => {
    await mockNoProfileOnCatalysts(page)
    await setupMockedWallet(page, ethereumWalletMock, {
      privateKey: generatePrivateKey(),
      redirectTo: REDIRECT_TO
    })
    const auth = new AuthPage(page)
    const qs = new QuickSetupPage(page)
    const landing = new LandingPage(page)

    await auth.clickMetaMaskButton()

    await qs.waitFor()
    await qs.fillUsername(uniqueUsername())
    await qs.acceptTerms()

    // Drive the avatar toolbar: switch body type, randomize a few times,
    // switch back. Mirrors the codegen recording (skipping the accidental
    // Intercom click).
    await qs.selectBodyType('B')
    await qs.clickRandomize()
    await qs.clickRandomize()
    await qs.clickRandomize()
    await qs.selectBodyType('A')

    await qs.submit()
    await qs.clickStartExploring()
    await landing.waitForUrl()
    expect(page.url()).not.toMatch(/\/auth/)
  })
})
