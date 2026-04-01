namespace ExplorerAutomation.Tests.Views;

public class AuthenticationMainScreenView() :
    BaseView(new(By.NAME, "Authentication.MainScreen(Clone)"))
{
    public readonly Clickable JumpIntoWorldButton        = new(By.ID, "646623d5-3519-49df-93ed-ab668d7917db");
    public readonly Clickable UseADifferentAccountButton = new(By.ID, "f658ab9f-18ac-4281-a0a9-dd030d8224d6");
}