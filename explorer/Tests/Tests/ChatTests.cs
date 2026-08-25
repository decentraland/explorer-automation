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
        Views.MainMenu.ChatButton.Click(settleMs: 0);
        Views.Chat.ConversationsToolbar.WaitForGone();
        Assert.That(Views.Chat.InputPlaceholder.GetText(), Is.EqualTo(ChatPanelView.PLACEHOLDER_CLOSED),
            "Chat input should show 'Press Enter to chat' after closing");
        Reporter.Log("Chat closed by clicking the sidebar chat button again");
    }

    [Test]
    public void TestOpenChatWithEnterAndCloseWithEscape()
    {
        // Retry, not just delay: 0 + a single wait — a dropped Enter has no other signal to
        // recover from than pressing it again (harmless: the chat ignores an empty submit).
        ClickUntil(() => PressKey(AltKeyCode.Return, delay: 0),
                   () => Views.Chat.ConversationsToolbar.IsPresent(verificationShot: false));
        Views.Chat.ConversationsToolbar.WaitFor();
        Assert.That(Views.Chat.InputPlaceholder.GetText(), Is.EqualTo(ChatPanelView.PLACEHOLDER_OPEN),
            "Chat input should be focused after pressing Enter");
        Reporter.Log("Chat opened with the Enter key");

        PressEscape(delay: 0);
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
    /// Leaves the chat closed AND the sidebar toggle in sync after every test, so no test
    /// inherits the Escape-close desync from a predecessor (see OpenChat docs; no upstream
    /// issue filed for the desync as of 2026-08). The probe click resolves the toggle state
    /// deterministically: if the toggle was in sync the click opens the panel and we close
    /// it again via the button (still in sync); if it was desynced (Escape-close) the click
    /// is exactly the no-op that resyncs it.
    /// </summary>
    [TearDown]
    public void NormalizeChatState()
    {
        // Runs before BaseTest.TearDown (NUnit runs derived teardowns first): disarm
        // verification shots so this plumbing doesn't attach screenshots, and bail if the
        // fixture never made it in-world.
        Reporter.StopVerificationShots();
        if (ExceptionFromOneTimeSetUp != null)
            return;

        // If the test (or its failure path) left the chat open, close it via the sidebar
        // button — the path that keeps the toggle in sync.
        if (Views.Chat.ConversationsToolbar.IsPresent())
        {
            Views.MainMenu.ChatButton.Click();
            if (!TryWaitForToolbarGone(3))
            {
                PressEscape(delay: 0); // last resort; the probe below repairs the desync this causes
                TryWaitForToolbarGone(3);
            }
        }

        Views.MainMenu.ChatButton.Click();
        if (TryWaitForToolbar(2))
        {
            Views.MainMenu.ChatButton.Click();
            TryWaitForToolbarGone(3);
        }
        else
        {
            Reporter.Log("Probe click was a no-op — sidebar chat toggle was desynced (Escape-close bug), now resynced");
        }
    }

    /// <summary>
    /// Opens the chat via the sidebar button. The button's internal toggle state desyncs
    /// when the panel was last closed with Escape: the next click is consumed as a "close"
    /// no-op and only the following click reopens the panel (reproduced deterministically
    /// via UiDump on build dev_b97439fc — open/Escape/click cycles fail on every other
    /// click). NormalizeChatState resyncs the toggle after every test, so the retry here
    /// is a safety net for the first test of the fixture, not an expected path.
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
            // Shot-suppressed poll: the one shot fires on the frame the state was confirmed.
            // Nothing on the timeout path — the caller either clicks again or falls through to
            // an authoritative wait, and both of those capture.
            if (Views.Chat.ConversationsToolbar.IsPresent(verificationShot: false))
            {
                Reporter.TakeVerificationShot($"present_{Views.Chat.ConversationsToolbar.ShotName}");
                return true;
            }
            Wait(0.5);
        }

        return false;
    }

    /// <summary>
    /// Closes the chat with Escape. Retries because an overlay (e.g. the reaction selector)
    /// can swallow the first Escape; falls back to the sidebar chat button, which toggles
    /// the panel closed regardless of overlay focus.
    /// </summary>
    private void CloseChat()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            PressEscape(delay: 0);
            if (TryWaitForToolbarGone(3))
                return;

            Reporter.Log("Chat still open after Escape — an overlay likely consumed it, retrying");
        }

        Reporter.Log("Escape did not close the chat — falling back to the sidebar chat button");
        Views.MainMenu.ChatButton.Click(settleMs: 0);

        // Final authoritative wait so a genuine failure produces the standard error.
        Views.Chat.ConversationsToolbar.WaitForGone();
    }

    private bool TryWaitForToolbarGone(double seconds)
    {
        for (var elapsed = 0.0; elapsed < seconds; elapsed += 0.5)
        {
            // See TryWaitForToolbar: suppressed poll, one shot where the state is confirmed.
            if (!Views.Chat.ConversationsToolbar.IsPresent(verificationShot: false))
            {
                Reporter.TakeVerificationShot($"absent_{Views.Chat.ConversationsToolbar.ShotName}");
                return true;
            }
            Wait(0.5);
        }

        return false;
    }
}
