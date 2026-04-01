namespace ExplorerAutomation.Tests.Views;

public class ViewContainer
{
    public static ViewContainer Instance { get; private set; }

    public AuthenticationMainScreenView AuthenticationMainScreen { get; } = new();
    public SplashScreenView             SplashScreen             { get; } = new();
    public LoadingScreenView            LoadingScreen            { get; } = new();
    public MainMenuView                 MainMenu                 { get; } = new();
    public ExplorePanelView             ExplorePanel             { get; } = new();
    public PassportView                 Passport                 { get; } = new();

    [AllureBefore("Initialize View Objects")]
    public static void Initialize()
    {
        Instance = new ViewContainer();
    }
}
