import type { Page, Locator } from '@playwright/test'

/**
 * The "Authorize → Sign" modal opened by `withAuthorizedAction` (decentraland-dapps)
 * before any action that needs an ERC20/ERC721 approval to a marketplace operator —
 * listing for sale, bidding, accepting trades, placing orders, etc. The same UI
 * is reused across flows; only the second-step button label and the resulting
 * POST endpoint change.
 *
 * Source: decentraland-dapps/src/containers/withAuthorizedAction/AuthorizationModal
 *
 * Wallet-side, the flow is signature-only:
 *   Step 1 (Authorize): `eth_signTypedData_v4` for a meta-tx wrapper, dapp POSTs
 *     the signature to `transactions-api /v1/transactions`. Relayer broadcasts
 *     `setApprovalForAll` (or `approve` for ERC20) on the target chain.
 *     Sign enables once that POST returns OK — the dapp does NOT wait for the
 *     on-chain receipt before unlocking step 2.
 *   Step 2 (Sign): a second signature for the wrapped action itself (e.g. the
 *     trade for a listing). Dapp POSTs to the action-specific endpoint.
 *
 * If the wallet has already granted the relevant approval, the modal opens
 * with step 1 marked complete and Sign is enabled immediately.
 *
 * No data-testids exist on this modal yet — locators rely on button accessible
 * names from decentraland-dapps' translation file.
 *
 * TODO(testid): replace getByRole fallbacks once the marketplace exposes
 *   `auth-modal`, `auth-authorize-button`, `auth-sign-button`.
 */
export class AuthorizationModal {
  constructor(private readonly page: Page) {}

  authorizeButton(): Locator {
    return this.page.getByRole('button', { name: /^authorize$/i })
  }

  /**
   * Step 2's button. Label varies by wrapped action: "Sign" for listings,
   * "Confirm transaction" for buys. Match both so the same POM drives all
   * `withAuthorizedAction` flows.
   */
  signButton(): Locator {
    return this.page.getByRole('button', { name: /^(sign|confirm transaction)$/i })
  }

  /**
   * Drives the modal end-to-end if it opens. The modal is shown only when
   * the wallet hasn't yet granted the relevant approval — if approval is
   * already cached on-chain, the dapp's saga calls `onAuthorized` directly
   * and the modal never appears (the action proceeds straight to its
   * primary signature).
   *
   * `authorizeAndSign` reflects that: it waits up to `openTimeoutMs` for the
   * modal, and returns silently if it doesn't appear. Caller observes the
   * resulting `POST` to the action's own endpoint (e.g. `/v1/trades` for a
   * listing) regardless of whether the modal was driven or not.
   *
   * Returns `true` if the modal opened and was driven, `false` if it didn't
   * appear (already-approved wallet). The caller can use this to know how
   * many `/v1/transactions` POSTs to expect downstream (1 vs 2).
   */
  async authorizeAndSign(openTimeoutMs = 30_000): Promise<boolean> {
    const opened = await this.signButton()
      .waitFor({ state: 'visible', timeout: openTimeoutMs })
      .then(() => true)
      .catch(() => false)
    if (!opened) return false

    if (
      await this.authorizeButton()
        .isVisible()
        .catch(() => false)
    ) {
      await this.authorizeButton().click()
      // Step 2 unlocks once the relayer's auth POST returns OK. For some
      // approvals (notably ERC721 setApprovalForAll for listings) the
      // relayer can take significantly longer than for ERC20 approvals —
      // observed up to ~2 min in the wild. Cap the wait generously so the
      // listing flow doesn't fail spuriously while the modal is mid-sync.
      // Step 2's label varies by action ("Sign" or "Confirm transaction"),
      // so accept either.
      await this.page.waitForFunction(
        () => {
          const btn = Array.from(document.querySelectorAll('button')).find(b => {
            const text = (b.textContent ?? '').trim().toLowerCase()
            return text === 'sign' || text === 'confirm transaction'
          }) as HTMLButtonElement | undefined
          return btn != null && !btn.disabled
        },
        null,
        { timeout: 180_000 }
      )
    }
    await this.signButton().click()
    return true
  }
}
