import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'
import type { AssetType } from './AssetPage.js'

/**
 * "List for sale" form, served at `/contracts/<contract>/tokens/<tokenId>/sell`.
 *
 * The marketplace component is named `SellModal.tsx` (rendered inside
 * `SellPage.tsx`) but the surface is page-based, not modal-based — hence the
 * `goto()` method below.
 *
 * Source: marketplace/webapp/src/components/SellPage/SellModal/SellModal.tsx.
 *
 * No data-testids exist on the sell form yet. Locators rely on:
 *   - getByLabel for form fields (Price, Expiration date)
 *   - getByRole('button', { name }) for actions
 *   - getByRole('dialog') to scope the post-submit ConfirmInputValueModal
 *
 * Translation keys (en):
 *   sell_page.title           → "List for sale"          (page header, h1)
 *   sell_page.price           → "Price"                  (price field label)
 *   sell_page.expiration_date → "Expiration date"        (date field label)
 *   sell_page.submit          → "List for sale"          (submit button — same string as header)
 *   sell_page.confirm.title   → "Please confirm"         (confirm modal title)
 *   global.price              → "Price"                  (re-enter price label in confirm modal)
 *   global.proceed            → "Proceed"                (confirm button)
 *
 * TODO(testid): replace getByLabel/getByRole fallbacks once marketplace exposes
 * `sell-price-input`, `sell-expiration-input`, `sell-submit-button`,
 * `sell-confirm-dialog`, `sell-confirm-price-input`, `sell-confirm-proceed`.
 * Tracked in AGENTS.md → "Pending marketplace testids".
 */
export class SellModal {
  constructor(private readonly page: Page) {}

  async goto(type: AssetType, contractAddress: string, tokenId: string): Promise<void> {
    const segment = type === 'item' ? 'items' : 'tokens'
    await this.page.goto(withEnv(`contracts/${contractAddress}/${segment}/${tokenId}/sell`))
  }

  /**
   * The form's price field. Marketplace's `ManaField` doesn't render a
   * proper `<label for=...>` association, so `getByLabel` doesn't match —
   * the only stable accessible name on the textbox is its placeholder
   * (`"1000"` on the form, the user-entered price on the confirm dialog).
   * Anchor on the placeholder; the confirm dialog's price field is scoped
   * separately via `confirmPriceInput`.
   */
  priceInput(): Locator {
    return this.page.getByPlaceholder('1000')
  }

  expirationInput(): Locator {
    return this.page.getByPlaceholder('YYYY-MM-DD')
  }

  /**
   * Form's submit button. The page header is also "List for sale" but renders
   * as a heading, so role=button disambiguates safely.
   *
   * Note: ChainButton wraps this in a regular <button> and disables it via
   * style overrides when the wallet's `eth_chainId` doesn't match the NFT's
   * chain. The dev marketplace issues a `wallet_switchEthereumChain` to the
   * NFT's chain when the user interacts with the form; auth-identity.ts'
   * handler updates `activeChainHex` so subsequent `eth_chainId` calls match,
   * re-enabling the button. No explicit switch action is needed here.
   */
  submitButton(): Locator {
    return this.page.getByRole('button', { name: /^list for sale$/i })
  }

  /**
   * Post-submit ConfirmInputValueModal — asks the seller to retype the price.
   *
   * Semantic-ui-react's <Modal> doesn't render with `role="dialog"`, so
   * `getByRole('dialog')` doesn't match. The modal mounts at the page root
   * with className "ConfirmInputValueModal" (set in
   * marketplace/webapp/src/components/ConfirmInputValueModal/ConfirmInputValueModal.tsx).
   * TODO(testid): replace with `getByTestId('sell-confirm-dialog')` once
   * marketplace exposes one.
   */
  confirmDialog(): Locator {
    return this.page.locator('.ConfirmInputValueModal')
  }

  confirmPriceInput(): Locator {
    return this.confirmDialog().getByRole('textbox').first()
  }

  /**
   * "Proceed" only exists inside the confirm modal — no other surface on the
   * sell page renders that label, so we don't need to scope to the dialog.
   */
  confirmProceedButton(): Locator {
    return this.page.getByRole('button', { name: /^proceed$/i })
  }

  async waitForLoaded(timeoutMs = 30_000): Promise<void> {
    await this.priceInput().waitFor({ state: 'visible', timeout: timeoutMs })
  }

  /**
   * Fills the price, submits the form, retypes the price in the confirm
   * dialog, and clicks Proceed. After this, an `AuthorizationModal` opens —
   * drive it via the `authModal` fixture's `authorizeAndSign()`, then
   * observe the resulting `POST /v1/trades`.
   *
   * Submitting via Enter rather than clicking the ChainButton: clicking the
   * type="submit" button inside the semantic-ui `<Form>` did not reliably
   * fire `onSubmit` in headed Chromium runs (button became `[active]` but
   * `setShowConfirm(true)` never ran). Pressing Enter from the focused price
   * input triggers a native form submission that always fires.
   */
  async fillAndSubmit(priceMana: string): Promise<void> {
    await this.priceInput().fill(priceMana)
    await this.priceInput().press('Enter')
    await this.confirmDialog().waitFor({ state: 'visible', timeout: 15_000 })
    await this.confirmPriceInput().fill(priceMana)
    await this.confirmProceedButton().click()
  }
}
