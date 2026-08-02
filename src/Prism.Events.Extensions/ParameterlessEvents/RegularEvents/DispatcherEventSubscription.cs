namespace Prism.Events.Extensions;

/// <summary>
/// An <see cref="EventSubscription"/> that dispatches the subscriber's
/// <see cref="System.Action"/> through a <see cref="SynchronizationContext"/>, corresponding to
/// <see cref="ThreadOption.UIThread"/>.
/// </summary>
internal class DispatcherEventSubscription : EventSubscription
{
    private readonly SynchronizationContext synchronizationContext;

    /// <summary>
    /// Initializes a new instance of <see cref="DispatcherEventSubscription"/>.
    /// </summary>
    /// <param name="actionReference">A reference to the subscriber callback.</param>
    /// <param name="synchronizationContext">
    /// The synchronization context through which the callback is dispatched.
    /// </param>
    public DispatcherEventSubscription(IDelegateReference actionReference, SynchronizationContext synchronizationContext)
        : base(actionReference)
    {
        this.synchronizationContext = synchronizationContext;
    }

    /// <summary>
    /// Posts the subscriber callback to the configured synchronization context.
    /// </summary>
    /// <remarks>
    /// The method returns after posting the callback. If the weakly referenced callback is no longer
    /// alive, no work is posted.
    /// </remarks>
    public override void InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
            synchronizationContext.Post((o) => action(), null);
        }
    }
}
