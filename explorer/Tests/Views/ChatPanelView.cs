namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the chat panel docked to the lower-left of the HUD (nearby channel, message
/// history, input box, conversations toolbar). The ChatPanel GameObject stays enabled even
/// while the panel is collapsed — the collapsed/open state is only observable through:
///   - <see cref="ConversationsToolbar"/>: enabled only while the panel is open/focused, and
///   - <see cref="InputPlaceholder"/>: "Press Enter to chat" collapsed vs "Write a message" open.
/// The panel opens by clicking the sidebar chat button or pressing Enter, and closes with
/// Escape or a second sidebar click (verified live on build dev_b97439fc).
/// </summary>
public class ChatPanelView() : BaseView(new(By.NAME, "ChatPanel"))
{
    #region Elements

    public const string PLACEHOLDER_CLOSED = "Press Enter to chat";
    public const string PLACEHOLDER_OPEN   = "Write a message";

    // The chat message list is a pooled/virtualized scroll view: entry sibling order is NOT
    // guaranteed to be chronological once tiles are reused, so "last own entry" locators
    // ([-1]) are only reliable for interactions, not for asserting which message is newest.
    // For text asserts use HasOwnMessageContaining, which scans every enabled own entry.
    private const string OWN_ENTRY_PATH = "//ChatMessages/Viewport/Content/ChatEntry_Own(Clone)";

    public readonly Writable  MessageInput           = new(By.PATH, "//ChatInputBox//CustomInputField");
    public readonly Readable  InputPlaceholder       = new(By.PATH, "//ChatInputBox//Placeholder");
    // Enabled only while the input is focused (panel open).
    public readonly Clickable EmojiButton            = new(By.PATH, "//ChatInputBox//EmojiButton");

    // Vertical conversation-tabs bar on the right edge of the panel ("in-chat navbar").
    public readonly Locatable ConversationsToolbar   = new(By.NAME, "ChatConversationsToolbar2");
    public readonly Clickable NearbyConversationItem = new(By.PATH, "//ChatConversationsToolbar2//ChatConversationsToolbarViewItem(Clone)");

    // Nearby channel indicator in the titlebar (icon + "Nearby" label). Most titlebar
    // controls ([BTN] Close, [BTN] Members, [TXT] ...) have bracketed GameObject names that
    // AltTester's locator grammar cannot parse, so this clean-named container is the
    // reliable nearby-channel presence signal.
    public readonly Locatable NearbyChannelIndicator = new(By.NAME, "NearbyInfoContainer_NEW");

    public readonly Locatable ReactionSelector    = new(By.NAME, "ChatMessageReactionSelector");
    public readonly Clickable FirstReactionOption = new(By.PATH, "//ChatMessageReactionSelector//ChatReactionItem(Clone)");

    #endregion

    #region Helper methods

    /// <summary>
    /// Sends a chat message and waits until it shows up in the history. AltTester's
    /// SetText(submit: true) fires the TMP submit path twice on this build and posts the
    /// message twice, so instead we set the text without submitting and press Return.
    /// The Return occasionally gets dropped (input focus race), hence the retry: a retry
    /// Return after a successful send is harmless because the chat clears the input field
    /// on send and ignores empty submissions.
    /// </summary>
    /// <param name="text">Raw text to type into the input field.</param>
    /// <param name="expectedRenderedFragment">
    /// Fragment expected in the rendered entry when the chat rewrites the message
    /// (e.g. URLs render as "&lt;link=url&gt;..." markup). Defaults to <paramref name="text"/>.
    /// </param>
    [AllureStep("Send chat message")]
    public void SendMessage(string text, string expectedRenderedFragment = null)
    {
        var expected = expectedRenderedFragment ?? text;
        MessageInput.SetText(text, submit: false);

        const int SEND_ATTEMPTS = 3;
        for (var attempt = 1; attempt <= SEND_ATTEMPTS; attempt++)
        {
            CommonStuff.AltDriver.PressKey(AltKeyCode.Return);

            for (var poll = 0; poll < 6; poll++)
            {
                Thread.Sleep(500);
                // Shot-suppressed probe inside the poll loop — the single verification shot
                // is taken below, once the message is confirmed visible.
                if (HasOwnMessageContaining(expected, verificationShot: false))
                {
                    Reporter.Log($"Message '{text}' visible in chat history (attempt {attempt})");
                    Reporter.TakeVerificationShot("present_OwnChatMessage");
                    return;
                }
            }

            Reporter.Log($"Message not visible after Return attempt {attempt} — retrying");
        }

        throw new AssertionException(
            $"Chat message '{text}' did not appear in the history after {SEND_ATTEMPTS} send attempts");
    }

    /// <summary>
    /// True if any currently loaded own chat entry contains the given rendered-text fragment.
    /// This scan bypasses the hooked element layer (raw FindObjects + GetText), so it attaches
    /// its own verification shot for either outcome, mirroring <see cref="Locatable.IsPresent()"/>.
    /// </summary>
    public bool HasOwnMessageContaining(string fragment) => HasOwnMessageContaining(fragment, verificationShot: true);

    // Shot-suppressed overload for the SendMessage poll loop — per-poll probes must not capture.
    [AllureStep("Check chat history for own message")]
    private bool HasOwnMessageContaining(string fragment, bool verificationShot)
    {
        var found = FindOwnMessageIndex(fragment) >= 0;
        if (verificationShot)
            Reporter.TakeVerificationShot($"{(found ? "present" : "absent")}_OwnChatMessage");
        return found;
    }

    /// <summary>
    /// Adds the first available quick reaction to the own message containing the given text
    /// and waits for the reaction count row to appear under it. The entry is anchored by
    /// index-of-matching-text rather than [-1] because the pooled message list re-binds
    /// entries when nearby messages arrive, which silently moves a positional anchor.
    /// Hover state persists for the lifetime of the driver session, so PointerEnter + click
    /// work in sequence here. One retry, because an incoming nearby message can re-bind the
    /// pool between the index lookup and the click.
    /// </summary>
    [AllureStep("React to own chat message")]
    public void ReactToOwnMessage(string messageFragment)
    {
        const int ATTEMPTS = 2;
        for (var attempt = 1; attempt <= ATTEMPTS; attempt++)
        {
            var index = FindOwnMessageIndex(messageFragment);
            if (index < 0)
                throw new AssertionException($"No own chat entry containing '{messageFragment}' to react to");

            var entryPath = $"{OWN_ENTRY_PATH}[{index}]";

            // The whole hover-through-pick sequence is inside the retry now, not just the
            // reaction-row check below: a click any one of these waits was guarding can be
            // dropped by the same pool re-bind, and only retrying from the hover recovers it.
            try
            {
                // A hiccup-triggered performance prompt can also be what ate the previous
                // attempt's click — clear it before trying again.
                PerformanceIssuePrompt.DismissIfPresent();
                new Clickable(By.PATH, entryPath + "/MessageBubbleElement").WaitFor().PointerEnter();
                var emojiSelectorButton = new Clickable(By.PATH, entryPath + "//EmojiSelectorButton");
                emojiSelectorButton.WaitFor(5); // wait for the hover-revealed reaction button to enable
                emojiSelectorButton.Click();
                ReactionSelector.WaitFor(10);
                FirstReactionOption.Click();
                Reporter.Log($"Picked the first quick reaction for entry {index} (attempt {attempt})");

                new Locatable(By.PATH, entryPath + "/ChatReactionsRow_Own(Clone)").WaitFor(10);
                DismissReactionSelectorIfOpen();
                return;
            }
            catch when (attempt < ATTEMPTS)
            {
                Reporter.Log("Reaction did not land — click likely dropped or pool re-bound, retrying");
            }
        }
    }

    /// <summary>
    /// The reaction selector popup can stay open after a reaction is picked, and while it is
    /// up it swallows the next Escape — which made a following close-chat Escape a no-op.
    /// Dismiss it here so callers are never left with the overlay armed.
    /// </summary>
    private void DismissReactionSelectorIfOpen()
    {
        if (!ReactionSelector.IsPresent())
            return;

        Reporter.Log("Reaction selector still open after reacting — dismissing it with Escape");
        CommonStuff.AltDriver.PressKey(AltKeyCode.Escape);
        ReactionSelector.WaitForGone(5);
    }

    private int FindOwnMessageIndex(string fragment)
    {
        var entries = CommonStuff.AltDriver.FindObjects(By.PATH, OWN_ENTRY_PATH + "/MessageBubbleElement/MessageContentElement");
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].GetText().Contains(fragment))
                return i;
        }

        return -1;
    }

    #endregion
}
