namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Email + OTP Login")]
[Category("Auth")]
// MUST stay last — see comment on EmailOtpLoginTests.
[Order(1001)]
public class EmailOtpLoginWithNewsletterTests : LoggedOutAuthBaseTest
{
    [Test]
    public void TestNewUserCanLoginWithEmailOtpAndSubscribeToNewsletter()
    {
        // Step 1 — submit email. Each call to GenerateFreshEmail returns a brand-new
        // qa-<hash>@<EMAIL_DOMAIN> recipient (its own per-address rate-limit bucket),
        // so we can retry transparently on any transient failure.
        var email = SubmitEmailWithRetry(OtpMailbox.GenerateFreshEmail);

        // Step 2 — fetch OTP from inbox and submit
        var code = OtpMailbox.WaitForOtp(email);
        Views.OtpVerificationScreen.OtpInput.SetText(code);

        // Step 3 — new-user setup with newsletter opt-in. Insert short waits between actions:
        // the screen animates in and the form-validity logic (which gates the JumpIn button)
        // reacts a beat after each interaction, so back-to-back clicks can land on a
        // non-yet-enabled control.
        Views.WelcomeNewAccountScreen.WaitFor(60);
        Wait(1);
        var username = "QA" + Guid.NewGuid().ToString("N")[..6];
        Views.WelcomeNewAccountScreen.UsernameInput.SetText(username);
        Wait(1);
        Views.WelcomeNewAccountScreen.SubscribeToggle.Click();
        Wait(1);
        Views.WelcomeNewAccountScreen.TermsOfUseToggle.Click();
        Wait(1);
        Views.WelcomeNewAccountScreen.RandomizeButton.Click();
        Wait(1);
        Views.WelcomeNewAccountScreen.JumpInButton.Click();
        Reporter.Log($"New account created with username '{username}', subscribed to newsletter, avatar randomized, jumping in");

        // Step 4 — wait for in-world via the LoadingScreen + SidebarView signal.
        WaitForInWorldAfterJumpIn();

        // Step 5 — open backpack via the shortcut and verify.
        OpenExplorePanelViaShortcut();
        Assert.That(Views.ExplorePanel.Backpack.IsPresent(), Is.True,
            "Backpack section should be visible after pressing I");
        PressEscape();
        Views.ExplorePanel.WaitForGone();

        // Step 6 — jump.
        PressKey(AltKeyCode.Space);
        Wait(1);
        Reporter.Log("Jump issued");
    }
}
