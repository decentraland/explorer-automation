namespace ExplorerAutomation.Tests.Views;

public abstract class BaseView(Locatable root)
{
    public virtual AltObject WaitFor(double timeout = 20D) => root.WaitFor(timeout);

    public virtual void WaitForGone(double timeout = 20D) => root.WaitForGone(timeout);

    public virtual bool IsPresent() => root.IsPresent();

}

public abstract class BaseClickableView(Clickable root) : BaseView(root)
{
    public virtual void Click() => root.Click();
}