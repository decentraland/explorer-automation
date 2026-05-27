namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// Singleton container that holds all top-level view instances used by tests.
/// Initialized once per test run via <see cref="Initialize"/> and accessed through <see cref="Instance"/>.
/// Tests should reference views through this container rather than creating their own instances.
/// </summary>
public class ViewContainer
{
    public static ViewContainer Instance { get; private set; }

    public AuthenticationMainScreenView AuthenticationMainScreen { get; } = new();
    public MinimumSpecsScreenView       MinimumSpecsScreen       { get; } = new();
    public OtpVerificationScreenView    OtpVerificationScreen    { get; } = new();
    public WelcomeNewAccountScreenView  WelcomeNewAccountScreen  { get; } = new();
    public SplashScreenView             SplashScreen             { get; } = new();
    public LoadingScreenView            LoadingScreen            { get; } = new();
    public MainMenuView                 MainMenu                 { get; } = new();
    public ProfileMenuView              ProfileMenu              { get; } = new();
    public ExplorePanelView             ExplorePanel             { get; } = new();
    public PassportView                 Passport                 { get; } = new();

    [AllureBefore("Initialize View Objects")]
    public static void Initialize()
    {
        Instance = new ViewContainer();
    }
}
