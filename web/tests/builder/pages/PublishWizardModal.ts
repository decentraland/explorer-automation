import type { Page, Locator } from '@playwright/test'

/**
 * Standard-collection publish wizard (components/Modals/PublishWizardCollectionModal).
 * Steps: confirm name → confirm items → content policy → pay fee → congratulations.
 *
 * Locator ← builder source mapping (i18n: publish_wizard_collection_modal.*):
 *  - root `.ui.modal.PublishWizardCollectionModal` ← explicit modal class
 *  - name confirmation field  ← confirm_collection_name_step.collection_name_placeholder
 *                                "Your collection name"; submit "Confirm Name"
 *  - items step submit        ← confirm_collection_items_step.confirm_items "Confirm Items"
 *  - policy checkboxes        ← ReviewContentPolicyStep renders the N required condition
 *                                checkboxes first, then the email Field (type=email), then the
 *                                optional newsletter checkbox LAST — the DOM order is the
 *                                contract for checking all-but-last
 *  - continue                 ← data-testid review-content-policy-continue-data-test-id
 *                                (one of the builder's few real testids)
 *  - "pay with mana"          ← pay_publication_fee_step.pay_mana (NetworkButton, Polygon)
 *  - success                  ← congratulations_step.title "Your collection is now available
 *                                for curators to review"
 */
export class PublishWizardModal {
  constructor(private readonly page: Page) {}

  root(): Locator {
    return this.page.locator('.ui.modal.PublishWizardCollectionModal')
  }

  nameConfirmationField(): Locator {
    return this.root().getByPlaceholder('Your collection name')
  }

  confirmNameButton(): Locator {
    return this.root().getByRole('button', { name: 'Confirm Name' })
  }

  confirmItemsButton(): Locator {
    return this.root().getByRole('button', { name: 'Confirm Items' })
  }

  policyCheckboxes(): Locator {
    return this.root().locator('.ui.checkbox')
  }

  emailField(): Locator {
    return this.root().locator('input[type="email"]')
  }

  continueButton(): Locator {
    return this.root().getByTestId('review-content-policy-continue-data-test-id')
  }

  payWithManaButton(): Locator {
    return this.root().getByRole('button', { name: 'pay with mana' })
  }

  successMessage(): Locator {
    return this.root().getByText('Your collection is now available for curators to review')
  }

  /** Checks every required condition checkbox, skipping the trailing newsletter opt-in. */
  async acceptContentPolicy(email: string): Promise<void> {
    const count = await this.policyCheckboxes().count()
    for (let i = 0; i < count - 1; i++) {
      await this.policyCheckboxes().nth(i).click()
    }
    await this.emailField().fill(email)
  }
}
