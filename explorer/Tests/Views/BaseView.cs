namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// Abstract base class for all view objects in the Page Object Model.
/// Wraps a root <see cref="Locatable"/> element and provides common wait and presence-check methods.
/// All views representing a screen or UI region should inherit from this class.
/// </summary>
public abstract class BaseView(Locatable root)
{
    /// <summary>
    /// Waits for the view's root element to appear in the scene.
    /// </summary>
    /// <param name="timeout">Maximum seconds to wait before throwing.</param>
    /// <returns>The <see cref="AltObject"/> representing the root element.</returns>
    public virtual AltObject WaitFor(double timeout = 20D) => root.WaitFor(timeout);

    /// <summary>
    /// Waits for the view's root element to disappear from the scene.
    /// </summary>
    /// <param name="timeout">Maximum seconds to wait before throwing.</param>
    public virtual void WaitForGone(double timeout = 20D) => root.WaitForGone(timeout);

    /// <summary>
    /// Checks whether the view's root element is currently present in the scene.
    /// Does not wait or throw — returns immediately.
    /// </summary>
    /// <returns><c>true</c> if the root element exists; otherwise <c>false</c>.</returns>
    public virtual bool IsPresent() => root.IsPresent();
}

/// <summary>
/// Abstract base class for views whose root element is clickable.
/// Extends <see cref="BaseView"/> with a <see cref="Click"/> action.
/// Used for small, interactive UI elements that act as both a view and a click target
/// (e.g. emote slots, grid items).
/// </summary>
public abstract class BaseClickableView(Clickable root) : BaseView(root)
{
    /// <summary>
    /// Clicks the view's root element after waiting for it to appear.
    /// </summary>
    public virtual void Click() => root.Click();
}