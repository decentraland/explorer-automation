namespace ExplorerAutomation.Tests.Tests;

// Checklist items that need a second logged-in user and therefore cannot be automated solo:
//   - Direct messages (needs a friend/second account to converse with).
//   - Mentioning another user / being mentioned (the @-suggestion box only reacts to real
//     typed input events, not AltTester SetText, and a meaningful mention needs a recipient).
//   - Seeing connected users around: the member-list button and counter exist, but their
//     GameObject names ("[BTN] Members", "[TXT] MembersCount") contain brackets that
//     AltTester's locator grammar cannot parse (verified live — By.NAME and By.PATH both
//     throw on them), so the member list cannot be opened or read from a locator.
// Text-style change: this build's chat has no formatting control (only an emoji button),
// so there is nothing to automate for it.
[AllureSuite("Chat Tests")]
[Category("InWorld")]
[Order(14)]
public class ChatTests : BaseTest
{
    private const string PING_MESSAGE = "automation ping";

    [Test]
    public void TestOpenAndCloseChatFromSidebar()
    {
        OpenChat();
        Assert.That(Views.Chat.InputPlaceholder.GetText(), Is.EqualTo(ChatPanelView.PLACEHOLDER_OPEN),
            "Chat input should be focused (placeholder 'Write a message') after opening from the sidebar");
        Reporter.Log("Chat opened from the sidebar chat button");

        // The sidebar chat button toggles: a second click closes the panel again.
        Views.MainMenu.ChatButton.Click();
        Views.Chat.ConversationsToolbar.WaitForGone();
        Assert.That(Views.Chat.InputPlaceholder.GetText(), Is.EqualTo(ChatPanelView.PLACEHOLDER_CLOSED),
            "Chat input should show 'Press Enter to chat' after closing");
        Reporter.Log("Chat closed by clicking the sidebar chat button again");
    }

    [Test]
    public void TestOpenChatWithEnterAndCloseWithEscape()
    {
        PressKey(AltKeyCode.Return);
        Views.Chat.ConversationsToolbar.WaitFor();
        Assert.That(Views.Chat.InputPlaceholder.GetText(), Is.EqualTo(ChatPanelView.PLACEHOLDER_OPEN),
            "Chat input should be focused after pressing Enter");
        Reporter.Log("Chat opened with the Enter key");

        PressEscape();
        Views.Chat.ConversationsToolbar.WaitForGone();
        Reporter.Log("Chat closed with Escape");
    }

    [Test]
    public void TestSendMessageAppearsInHistory()
    {
        OpenChat();

        Views.Chat.SendMessage(PING_MESSAGE);
        Assert.That(Views.Chat.HasOwnMessageContaining(PING_MESSAGE), Is.True,
            $"Own chat entry with text '{PING_MESSAGE}' should be visible in the history");
        Reporter.Log("Sent message is visible in the nearby chat history");

        CloseChat();
    }

    [Test]
    public void TestSendHyperlinkRendersAsLink()
    {
        OpenChat();

        // The chat renders URLs as colored TMP link markup; assert on the rendered fragment.
        Views.Chat.SendMessage("https://decentraland.org",
            expectedRenderedFragment: "<link=url>https://decentraland.org</link>");
        Reporter.Log("URL message rendered with <link=url> markup (clickable hyperlink)");

        CloseChat();
    }

    [Test]
    public void TestNearbyChannelAndConversationsToolbarPresence()
    {
        OpenChat();

        Assert.That(Views.Chat.NearbyChannelIndicator.IsPresent(), Is.True,
            "Titlebar should show the Nearby channel indicator");
        Assert.That(Views.Chat.NearbyConversationItem.IsPresent(), Is.True,
            "Conversations toolbar should contain the Nearby conversation tab");
        Assert.That(Views.Chat.MessageInput.IsPresent(), Is.True,
            "Chat input field should be present");
        Assert.That(Views.Chat.EmojiButton.IsPresent(), Is.True,
            "Emoji button should be present on the chat input");
        Reporter.Log("Nearby channel, conversations toolbar, input and emoji button all present");

        CloseChat();
    }

    [Test]
    public void TestReactToOwnMessage()
    {
        OpenChat();

        // Send a run-unique message first so the reaction lands on a fresh test message even
        // when this test runs standalone (reacting to an already-reacted message would
        // toggle the reaction off instead).
        var message = $"automation ping {DateTime.Now:HHmmss}";
        Views.Chat.SendMessage(message);
        Views.Chat.ReactToOwnMessage(message);
        Reporter.Log("Reaction row appeared under the own message");

        CloseChat();
    }

    /// <summary>
    /// Opens the chat via the sidebar button. The button's internal toggle state desyncs
    /// when the panel was last closed with Escape: the next click is consumed as a "close"
    /// no-op and only the following click reopens the panel (reproduced deterministically
    /// via UiDump on build dev_b97439fc — open/Escape/click cycles fail on every other
    /// click). Hence the retry.
    /// </summary>
    private void OpenChat()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Views.MainMenu.ChatButton.Click();
            if (TryWaitForToolbar(4))
                return;

            Reporter.Log("Chat did not open — sidebar toggle state was desynced, clicking again");
        }

        // Final authoritative wait so a genuine failure produces the standard error.
        Views.Chat.ConversationsToolbar.WaitFor();
    }

    private bool TryWaitForToolbar(double seconds)
    {
        for (var elapsed = 0.0; elapsed < seconds; elapsed += 0.5)
        {
            if (Views.Chat.ConversationsToolbar.IsPresent())
                return true;
            Wait(0.5);
        }

        return false;
    }

    private void CloseChat()
    {
        PressEscape();
        Views.Chat.ConversationsToolbar.WaitForGone();
    }
}
