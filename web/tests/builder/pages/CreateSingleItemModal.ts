import type { Page, Locator } from '@playwright/test'

/**
 * "New Item" modal (components/Modals/CreateSingleItemModal) — the item upload
 * wizard. Step order: IMPORT → DETAILS (wearable/emote) with smart wearables
 * routed IMPORT → UPLOAD_VIDEO → DETAILS, and emotes gaining an extra
 * thumbnail-screenshot step (EditThumbnailStep) entered via "next".
 *
 * Locator ← builder source mapping:
 *  - root `.ui.modal.CreateSingleItemModal`  ← modal registry name
 *  - file input `.FileImport input[type=file]` ← components/FileImport dropzone (also serves the
 *                                                video step — only one FileImport is mounted at a time)
 *  - name field                              ← create_single_item_modal.name_label "Enter a name for your item"
 *  - rarity select                           ← create_single_item_modal.rarity_label "What's this item's rarity?"
 *                                              (options text: wearable.rarity.* e.g. "Common")
 *  - category select                         ← create_single_item_modal.category_label "What's the category of this item?"
 *  - play-mode select (emotes)               ← create_single_item_modal.play_mode_label "Play mode"
 *  - body-shape options `.option.has-icon.*` ← WearableDetails renderRepresentation (both/male/female)
 *  - "Save" / "next"                         ← global.save / global.next (EmoteDetails shows "next" until the
 *                                              thumbnail screenshot is taken; EditThumbnailStep's "Save" enables
 *                                              once the WearablePreview iframe reports an update)
 *  - size errors                             ← create_single_item_modal.error.file_too_big_title / item_too_big
 */
export class CreateSingleItemModal {
  constructor(private readonly page: Page) {}

  root(): Locator {
    return this.page.locator('.ui.modal.CreateSingleItemModal')
  }

  fileInput(): Locator {
    return this.root().locator('.FileImport input[type=file]')
  }

  /** Upload a model (or video, on the video step) through the dropzone input. */
  async uploadFile(filePath: string | { name: string; mimeType: string; buffer: Buffer }): Promise<void> {
    await this.fileInput().setInputFiles(filePath)
  }

  // decentraland-ui renders text Fields as `.dcl.field` but SelectFields as
  // `.dcl.select-field` (label + Semantic dropdown inside) — two different
  // root classes, hence two lookup helpers.
  private field(labelText: string): Locator {
    return this.root().locator('.dcl.field').filter({ hasText: labelText })
  }

  private selectField(labelText: string): Locator {
    return this.root().locator('.dcl.select-field').filter({ hasText: labelText })
  }

  nameField(): Locator {
    return this.field('Enter a name for your item').locator('input')
  }

  raritySelect(): Locator {
    return this.selectField("What's this item's rarity?").locator('.ui.dropdown')
  }

  categorySelect(): Locator {
    return this.selectField("What's the category of this item?").locator('.ui.dropdown')
  }

  playModeSelect(): Locator {
    return this.selectField('Play mode').locator('.ui.dropdown')
  }

  bodyShapeOption(shape: 'both' | 'male' | 'female'): Locator {
    return this.root().locator(`.option.has-icon.${shape}`)
  }

  saveButton(): Locator {
    return this.root().getByRole('button', { name: 'Save' })
  }

  /** EmoteDetails submit label before the thumbnail screenshot exists. */
  nextButton(): Locator {
    return this.root().getByRole('button', { name: 'next' })
  }

  videoStepTitle(): Locator {
    return this.root().getByText('Upload a Video for your Smart Wearable')
  }

  fileTooBigError(): Locator {
    return this.root()
      .getByText(/too large/i)
      .first()
  }

  // TODO(testid): decentraland-ui ModalNavigation close affordance is a bare
  // div (.modal-navigation-close) — no role, no accessible name.
  closeButton(): Locator {
    return this.root().locator('.modal-navigation-close')
  }
}
