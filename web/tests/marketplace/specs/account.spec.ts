import { privateKeyToAddress } from 'viem/accounts'
import { walletTest as test } from '../fixtures/wallet-fixture.js'
import { injectAuthIdentity, installInjectedWalletMock } from '../../../shared/helpers/auth-identity.js'
import { mockExistingProfile } from '../../../shared/helpers/profile.js'
import { SYNPRESS_DEFAULT_KEY } from '../../../shared/helpers/synpress.js'
import { withEnv } from '../../../shared/helpers/url.js'

test.describe('@marketplace authenticated account view', () => {
  test('connected wallet sees their account page', async ({ page, navbar }) => {
    const baseURL = test.info().project.use.baseURL
    if (!baseURL) throw new Error('baseURL is required (Playwright project misconfigured)')

    const privateKey = SYNPRESS_DEFAULT_KEY
    const address = privateKeyToAddress(privateKey)

    await injectAuthIdentity(page, privateKey)
    await installInjectedWalletMock(page, privateKey)
    await mockExistingProfile(page, address)

    await page.goto(withEnv(`${baseURL.replace(/\/$/, '')}/account`))

    await navbar.waitForConnected(60_000)
    test.expect(page.url()).toMatch(/\/account/)
  })
})
