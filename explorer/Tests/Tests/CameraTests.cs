namespace ExplorerAutomation.Tests.Tests;

// In-world camera (photo mode). The regular HUD is hidden while photo mode is on, so the
// sidebar button can only open it — tests close with the C shortcut or Escape (Button_Close
// ignores synthetic clicks on this build, see InWorldCameraView.CloseButton).
[AllureSuite("In-World Camera Tests")]
[Category("InWorld")]
[Order(19)]
public class CameraTests : BaseTest
{
    [Test]
    public void TestToggleCameraWithShortcut()
    {
        PressKey(AltKeyCode.C);
        Views.InWorldCamera.WaitFor();
        Reporter.Log("Camera HUD opened with C");

        Views.InWorldCamera.TakeScreenshotButton.WaitFor();
        Views.InWorldCamera.CameraReelButton.WaitFor();
        Reporter.Log("SHOOT and gallery controls are visible");

        PressKey(AltKeyCode.C);
        Views.InWorldCamera.WaitForGone();
        Reporter.Log("Camera HUD closed with C");
    }

    [Test]
    public void TestOpenCameraFromSidebar()
    {
        Views.MainMenu.InWorldCameraButton.Click();
        Views.InWorldCamera.WaitFor();
        Reporter.Log("Camera HUD opened from the sidebar camera button");

        // The sidebar is hidden while photo mode is on, and when entered via the sidebar
        // button the fresh camera view eats keyboard input for a while (a C or Escape ~1s
        // after the HUD appears is swallowed) — CloseWithEscape retries until it lands.
        Views.InWorldCamera.CloseWithEscape();
    }

    [Test]
    public void TestTakePhoto()
    {
        // Capture alone does NOT touch ~/Downloads (no macOS TCC dialog): verified in the
        // unity-explorer sources — CaptureScreenshotSystem uploads the shot to the remote
        // camera reel service, and only the gallery's explicit "download reel" action
        // (ReelCommonActions.DownloadReelToFileAsync) writes under ~/Downloads.
        // Side effect: each run adds one photo to the shared dev account's camera reel.
        PressKey(AltKeyCode.C);
        Views.InWorldCamera.WaitFor();

        // The controller hides the SHOOT button when the reel storage quota is exhausted.
        if (!Views.InWorldCamera.TakeScreenshotButton.IsPresent())
            Assert.Ignore("Camera reel storage is full on the test account — cannot take more photos");

        PressKey(AltKeyCode.Space);
        Views.InWorldCamera.WaitForCaptureFx();
        Reporter.Log("Photo captured");

        PressKey(AltKeyCode.C);
        Views.InWorldCamera.WaitForGone();
    }
}
