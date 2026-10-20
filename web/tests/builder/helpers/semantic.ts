import type { Locator } from '@playwright/test'

/**
 * Selects an option in a Semantic-UI (decentraland-ui) dropdown. These render
 * as nested <div>s, not <select>, so Playwright's selectOption() cannot drive
 * them: click the trigger, then click the `.item` whose `.text` equals
 * `optionText` inside the dropdown's own `.menu` (a child of the trigger, not
 * a portal). Matching is exact-text on the option's deepest text element —
 * substring matching would confuse pairs like "Common"/"Uncommon", and option
 * rows can carry extra label text (e.g. rarity supply counts).
 */
export async function selectDropdownOption(dropdown: Locator, optionText: string): Promise<void> {
  await dropdown.click()
  await dropdown
    .locator('.menu > .item')
    .filter({ has: dropdown.page().getByText(optionText, { exact: true }) })
    .first()
    .click({ timeout: 10_000 })
}
