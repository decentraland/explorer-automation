import type { Page, Locator } from '@playwright/test'

/**
 * Curator approval flow (components/Modals/ApprovalFlowModal). For a standard
 * collection the views chain LOADING → RESCUE → DEPLOY → APPROVE → SUCCESS,
 * each advanced by its primary button once the previous transaction lands:
 *
 *  - rescue  ← approval_flow.rescue.confirm  "Confirm" (rescueItems meta-tx)
 *  - deploy  ← approval_flow.upload.confirm  "Upload"  (catalyst entity deploy)
 *  - approve ← approval_flow.approve.confirm "Enable"  (Committee setApproved meta-tx)
 *  - success ← approval_flow.success.title   "Collection Approved!" + global.close "Close"
 *
 * Root `.ui.modal.ApprovalFlowModal` ← explicit modal class.
 */
export class ApprovalFlowModal {
  constructor(private readonly page: Page) {}

  root(): Locator {
    return this.page.locator('.ui.modal.ApprovalFlowModal')
  }

  rescueConfirmButton(): Locator {
    return this.root().getByRole('button', { name: 'Confirm' })
  }

  uploadButton(): Locator {
    return this.root().getByRole('button', { name: 'Upload' })
  }

  enableButton(): Locator {
    return this.root().getByRole('button', { name: 'Enable' })
  }

  successTitle(): Locator {
    return this.root().getByText('Collection Approved!')
  }

  closeButton(): Locator {
    return this.root().getByRole('button', { name: 'Close' })
  }
}
