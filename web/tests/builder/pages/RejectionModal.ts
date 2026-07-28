import type { Page, Locator } from '@playwright/test'

/**
 * Curator rejection modal (ItemEditorPage/TopPanel/RejectionModal — explicit
 * `RejectionModal` class, not registry-driven). For REJECT_CURATION (the
 * server-side day-to-day path) the flow is Confirmation → Verdict:
 *
 *  - title  ← item_editor.top_panel.rejection_modal.reject_curation.title "Reject Changes"
 *  - action ← ….reject_curation.action "Reject" (NetworkButton; server-only PATCH here)
 *  - verdict ← ….veredict_explanation "Can you tell us why?" (nudges the curator to
 *    the forum thread — there is deliberately no free-text reason field)
 */
export class RejectionModal {
  constructor(private readonly page: Page) {}

  root(): Locator {
    return this.page.locator('.ui.modal.RejectionModal')
  }

  rejectButton(): Locator {
    return this.root().getByRole('button', { name: 'Reject', exact: true })
  }

  verdictMessage(): Locator {
    return this.root().getByText('Can you tell us why?')
  }
}
