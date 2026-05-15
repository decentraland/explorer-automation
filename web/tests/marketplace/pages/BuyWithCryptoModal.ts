import type { Page, Locator } from '@playwright/test'

/**
 * data-testid values verified in marketplace source:
 *   - pay-with-container        (PAY_WITH_DATA_TEST_ID)
 *   - chain-selector            (CHAIN_SELECTOR_DATA_TEST_ID)
 *   - token-selector            (TOKEN_SELECTOR_DATA_TEST_ID)
 *   - switch-network            (SWITCH_NETWORK_BUTTON_TEST_ID)
 *   - buy-now-button            (BUY_NOW_BUTTON_TEST_ID, inside the modal)
 *   - cross-chain-polling       (CROSS_CHAIN_POLLING_TEST_ID)
 *   - buy-with-card-button      (BUY_WITH_CARD_TEST_ID)
 */
export class BuyWithCryptoModal {
  constructor(private readonly page: Page) {}

  payWithContainer(): Locator {
    return this.page.getByTestId('pay-with-container')
  }

  chainSelector(): Locator {
    return this.page.getByTestId('chain-selector')
  }

  tokenSelector(): Locator {
    return this.page.getByTestId('token-selector')
  }

  switchNetworkButton(): Locator {
    return this.page.getByTestId('switch-network')
  }

  /** Final submit button inside the modal — triggers the sign + tx broadcast. */
  buyNowButton(): Locator {
    return this.page.getByTestId('buy-now-button')
  }

  crossChainPolling(): Locator {
    return this.page.getByTestId('cross-chain-polling')
  }

  async waitForOpen(timeoutMs = 30_000): Promise<void> {
    await this.payWithContainer().waitFor({ state: 'visible', timeout: timeoutMs })
  }

  async submitBuy(): Promise<void> {
    await this.buyNowButton().click()
  }

  /** Switches the wallet network if marketplace prompts for it. No-op if not shown. */
  async switchNetworkIfPrompted(timeoutMs = 5_000): Promise<void> {
    const visible = await this.switchNetworkButton()
      .isVisible()
      .catch(() => false)
    if (visible) await this.switchNetworkButton().click({ timeout: timeoutMs })
  }
}
