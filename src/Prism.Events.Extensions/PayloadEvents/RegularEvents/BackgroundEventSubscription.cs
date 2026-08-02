namespace Prism.Events.Extensions;

/// <summary>
/// An <see cref="EventSubscription{TPayload}"/> that dispatches filtered callbacks to the thread
/// pool, corresponding to <see cref="ThreadOption.BackgroundThread"/>.
/// </summary>
/// <typeparam name="TPayload">The type of payload delivered to the subscriber.</typeparam>
internal class BackgroundEventSubscription<TPayload> : EventSubscription<TPayload>
{
    /// <summary>
    /// Initializes a new instance of <see cref="BackgroundEventSubscription{TPayload}"/>.
    /// </summary>
    /// <param name="actionReference">A reference to the subscriber callback.</param>
    /// <param name="filterReference">A reference to the subscription filter.</param>
    public BackgroundEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference) :
        base(actionReference, filterReference)
    {

    }

    /// <summary>
    /// Queues filter evaluation and callback invocation to the thread pool.
    /// </summary>
    /// <param name="payload">The payload supplied by the publisher.</param>
    /// <remarks>
    /// The method returns after queuing the work. If the weakly referenced callback is no longer
    /// alive, no work is queued.
    /// </remarks>
    public override void InvokeAction(TPayload payload)
    {
        var action = Action;
        var filter = Filter;

        if (action != null)
        {
            _ = Task.Run(() =>
            {
                if (filter != null && filter(payload))
                {
                    action(payload);
                }
            });
        }
    }
}
