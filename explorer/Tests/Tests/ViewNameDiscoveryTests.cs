namespace ExplorerAutomation.Tests.Tests;

// Task 6 discovery fixture (view-explicit-signals sweep) — temporary, deleted once its log
// output has been read into the page-object -> client-view-name table. Walks every panel
// reachable from the sidebar and the explore panel's tabs without an OTP login, a wallet, or
// a marketplace purchase, logging a ViewSignal.Snapshot() after each stage. Deliberately
// skipped: Marketplace / Marketplace Credits / Friends / Nearby Voice / Smart Wearables
// sidebar buttons (external browser, unverified behaviour, or no page object to map), and
// the Places/Communities card-detail drilldowns (PlacesTests documents that clicking a place
// card's root rather than its thumbnail can teleport the shared account or mutate a favorite).
[TestFixture]
[Category("InWorld")]
public class ViewNameDiscoveryTests : BaseTest
{
    // Not a correctness wait - every real wait below is an existing view/element primitive.
    // This only gives the MVC lifecycle event a moment to reach the probe after the
    // GameObject it rides on becomes findable.
    private const int SETTLE_MS = 1000;

    [Test]
    public void DumpViewNamesWhileWalkingTheUi()
    {
        Reporter.Log($"VIEWNAMES probe_available: {ViewSignal.IsAvailable}");
        LogSnapshot("start");

        RunStage("notifications",
            action: () =>
            {
                Views.MainMenu.NotificationsButton.Click(settleMs: 0);
                Views.MainMenu.Notifications.WaitFor();
            },
            cleanup: () =>
            {
                Views.MainMenu.NotificationsButton.Click(settleMs: 0);
                Views.MainMenu.Notifications.WaitForGone();
            });

        RunStage("help_menu",
            action: () =>
            {
                Views.MainMenu.HelpButton.Click(settleMs: 0);
                Views.MainMenu.Help.WaitFor();
            });

        RunStage("controls_panel",
            action: () =>
            {
                // The menu's own show animation eats the first click on this build - same
                // guard as NavbarTests.TestOpenControlsPanelFromHelpMenu.
                ClickUntil(() => Views.MainMenu.Help.MouseAndKeyControlsButton.Click(settleMs: 0),
                    () => Views.ControlsPanel.IsPresent(verificationShot: false),
                    attempts: 2, timeoutPerAttempt: 20);
                Views.ControlsPanel.WaitFor();
            },
            cleanup: () =>
            {
                ClickUntil(() => Views.ControlsPanel.ExitButton.Click(settleMs: 0),
                    () => !Views.ControlsPanel.IsPresent(verificationShot: false),
                    attempts: 2, timeoutPerAttempt: 20);
                Views.ControlsPanel.WaitForGone();
                PressEscape(delay: 0); // clears the help menu if it is still open underneath
            });

        RunStage("skybox",
            action: () =>
            {
                Views.MainMenu.SkyboxButton.Click(settleMs: 0);
                Views.MainMenu.Skybox.WaitFor();
            },
            cleanup: () =>
            {
                PressEscape(delay: 0);
                Views.MainMenu.Skybox.WaitForGone();
            });

        RunStage("profile_menu",
            action: () =>
            {
                Views.MainMenu.ProfileButton.Click(settleMs: 0);
                Views.ProfileMenu.WaitFor().WaitForComponentProperty(
                    "UnityEngine.UI.GraphicRaycaster", "enabled", true, "UnityEngine.UI", timeout: 15);
            });

        RunStage("passport",
            action: () =>
            {
                Views.ProfileMenu.PreviewProfileButton.Click(settleMs: 0);
                Views.Passport.WaitUntilReady();
            },
            cleanup: () =>
            {
                Views.Passport.CloseButton.Click(settleMs: 0);
                Views.Passport.WaitForGone();
            });

        RunStage("chat",
            action: () =>
            {
                Views.MainMenu.ChatButton.Click(settleMs: 0);
                Views.Chat.ConversationsToolbar.WaitFor();
            },
            cleanup: () =>
            {
                // ChatPanel's own GameObject never toggles (see ChatPanelView) - the toolbar
                // is the only object-presence signal that the panel actually closed.
                PressEscape(delay: 0);
                Views.Chat.ConversationsToolbar.WaitForGone();
            });

        RunStage("emote_wheel",
            action: () =>
            {
                Views.MainMenu.EmoteWheelButton.Click(settleMs: 0);
                Views.EmotesWheel.WaitFor();
            },
            cleanup: () =>
            {
                PressEscape(delay: 0);
                Views.EmotesWheel.WaitForGone();
            });

        RunStage("inworld_camera",
            action: () =>
            {
                Views.MainMenu.InWorldCameraButton.Click(settleMs: 0);
                Views.InWorldCamera.WaitFor();
            },
            cleanup: () => Views.InWorldCamera.CloseWithEscape());

        RunStage("explore_backpack",
            action: () =>
            {
                Views.MainMenu.BackpackButton.Click(settleMs: 0);
                Views.ExplorePanel.Backpack.WaitFor();
            });

        RunStage("explore_events",
            action: () =>
            {
                Views.ExplorePanel.EventsTabButton.Click(settleMs: 0);
                Views.ExplorePanel.Events.WaitFor();
            });

        RunStage("event_detail",
            action: () =>
            {
                // Card resolution happens inside the retry, same as EventsTests.TestOpenEventDetail:
                // the calendar re-binds cards while events stream in.
                ClickUntil(() => Views.ExplorePanel.Events.FindTopLeftVisibleCard().Click(),
                    () => Views.ExplorePanel.Events.EventDetail.IsPresent(verificationShot: false));
                Views.ExplorePanel.Events.EventDetail.WaitFor();
            },
            cleanup: () =>
            {
                Views.ExplorePanel.Events.EventDetail.CloseButton.Click(settleMs: 0);
                Views.ExplorePanel.Events.EventDetail.WaitForGone();
            });

        RunStage("explore_places",
            action: () =>
            {
                Views.ExplorePanel.PlacesTabButton.Click(settleMs: 0);
                Views.ExplorePanel.Places.WaitFor();
            });

        RunStage("explore_communities",
            action: () =>
            {
                Views.ExplorePanel.CommunitiesTabButton.Click(settleMs: 0);
                Views.ExplorePanel.Communities.WaitFor();
            });

        RunStage("explore_map",
            action: () =>
            {
                Views.ExplorePanel.MapTabButton.Click(settleMs: 0);
                Views.ExplorePanel.Navmap.WaitFor();
            });

        RunStage("explore_gallery",
            action: () =>
            {
                Views.ExplorePanel.GalleryTabButton.Click(settleMs: 0);
                Views.ExplorePanel.Gallery.WaitFor();
            });

        RunStage("explore_settings",
            action: () =>
            {
                Views.ExplorePanel.SettingsTabButton.Click(settleMs: 0);
                Views.ExplorePanel.Settings.WaitFor();
            });

        RunStage("explore_closed", action: () => Views.ExplorePanel.Close());

        LogSnapshot("end");
    }

    /// <summary>
    /// Runs one stage of the walk and snapshots right after <paramref name="action"/>, so the
    /// log line shows the state that action produced. Every step is caught rather than thrown:
    /// this fixture gets one CI run to work with, so one stage failing must not cost the rest
    /// of the walk their snapshot.
    /// </summary>
    private void RunStage(string stage, Action action, Action cleanup = null)
    {
        try
        {
            action();
            Thread.Sleep(SETTLE_MS);
        }
        catch (Exception ex)
        {
            Reporter.Log($"VIEWNAMES {stage}: action failed - {ex.Message}");
        }

        LogSnapshot(stage);

        if (cleanup is null) return;
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            Reporter.Log($"VIEWNAMES {stage}: cleanup failed - {ex.Message}");
        }
    }

    private void LogSnapshot(string stage)
    {
        try
        {
            Reporter.Log($"VIEWNAMES {stage}: {ViewSignal.Snapshot()}");
        }
        catch (Exception ex)
        {
            Reporter.Log($"VIEWNAMES {stage}: snapshot call failed - {ex.Message}");
        }
    }
}
