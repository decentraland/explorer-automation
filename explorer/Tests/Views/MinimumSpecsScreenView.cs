namespace ExplorerAutomation.Tests.Views;

public class MinimumSpecsScreenView() : BaseView(new Locatable(By.NAME, "MinimumSpecsScreen(Clone)"))
{
    public readonly Clickable ContinueButton = new(By.NAME, "ContinueButton");
}
