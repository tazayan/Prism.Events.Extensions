namespace Prism.Events.Extensions;

/// <summary>
/// An <see cref="EventSubscription{TPayload}"/> that dispatches filtered callbacks through a
/// <see cref="SynchronizationContext"/>, corresponding to <see cref="ThreadOption.UIThread"/>.
/// </summary>
/// <typeparam name="TPayload">The type of payload delivered to the subscriber.</typeparam>
internal class DispatcherEventSubscription<TPayload> : EventSubscription<TPayload>
{
    private readonly SynchronizationContext synchronizationContext;

    /// <summary>
    /// Initializes a new instance of <see cref="DispatcherEventSubscription{TPayload}"/>.
    /// </summary>
    /// <param name="actionReference">A reference to the subscriber callback.</param>
    /// <param name="filterReference">A reference to the subscription filter.</param>
    /// <param name="synchronizationContext">
    /// The synchronization context through which the callback is dispatched.
    /// </param>
    public DispatcherEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference, SynchronizationContext synchronizationContext)
        : base(actionReference, filterReference)
    {
        this.synchronizationContext = synchronizationContext;
    }

    /// <summary>
    /// Posts filter evaluation and callback invocation to the configured synchronization context.
    /// </summary>
    /// <param name="payload">The payload supplied by the publisher.</param>
    /// <remarks>
    /// The method returns after posting the work. If the weakly referenced callback is no longer
    /// alive, no work is posted.
    /// </remarks>
    public override void InvokeAction(TPayload payload)
    {
        var action = Action;
        var filter = Filter;

        if (action != null)
        {
            synchronizationContext.Post((o) =>
            {
                if (filter != null && filter(payload))
                {
                    action(payload);
                }
            }, null);
        }
    }
}
