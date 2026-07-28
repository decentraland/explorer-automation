import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'

/**
 * Item editor (`/builder/item-editor?item=…`, components/ItemEditorPage).
 *
 * Locator ← builder source mapping:
 *  - `.emote-controls` ← CenterPanel.tsx footer passes className="emote-controls"
 *    to decentraland-ui2 EmoteControls; it renders ONLY when an emote is
 *    selected AND the WearablePreview controller is ready — its presence is
 *    itself the "preview loaded" signal.
 *  - play toggle ← EmoteControls StyledPlayButton; its MUI icon children carry
 *    automatic testids: PlayArrowIcon (idle/ended) / PauseIcon (playing).
 *    (The "Play Emote"/"Stop" Button.Group renders only for NON-emote items —
 *    it drives avatar animations, not the emote preview.)
 */
export class ItemEditorPage {
  constructor(private readonly page: Page) {}

  async goto(itemId: string, collectionId: string): Promise<void> {
    await this.page.goto(withEnv(`item-editor?item=${itemId}&collection=${collectionId}`))
  }

  /**
   * Curator review bar (ItemEditorPage/TopPanel — renders only for committee
   * members with ?reviewing=true). Buttons ← item_editor.top_panel.{approve,reject}.
   */
  approveButton(): Locator {
    return this.page.locator('.TopPanel').getByRole('button', { name: 'Approve' })
  }

  rejectButton(): Locator {
    return this.page.locator('.TopPanel').getByRole('button', { name: 'Reject' })
  }

  /**
   * RightPanel "Basics" description textarea (Collapsables default open;
   * the optional utility textarea renders after it, hence .first()).
   * TODO(testid): propose a testid for the description field.
   */
  descriptionField(): Locator {
    return this.page.locator('.RightPanel textarea').first()
  }

  /** RightPanel footer save (NetworkButton "Save", disabled until dirty). */
  saveButton(): Locator {
    return this.page.locator('.RightPanel').getByRole('button', { name: 'Save' })
  }

  emoteControls(): Locator {
    return this.page.locator('.emote-controls')
  }

  emotePlayToggle(): Locator {
    return this.emoteControls().locator('button').first()
  }

  /**
   * The playback scrubber — its value is the current animation frame and
   * advances while the emote plays. More reliable than the play/pause icon,
   * whose state depends on events echoed back from the preview iframe (the
   * pause-direction events were observed not to fire on deployed dev).
   */
  emoteFrameSlider(): Locator {
    return this.emoteControls().locator('input[type="range"]')
  }
}
