namespace Prism.Events.Extensions;

/// <summary>
/// An <see cref="AsyncEventSubscription{TPayload}"/> that schedules filter evaluation and callback
/// invocation through a <see cref="TaskScheduler"/>.
/// </summary>
/// <typeparam name="TPayload">The type of payload delivered to the subscriber.</typeparam>
internal class TaskEventSubscription<TPayload> : AsyncEventSubscription<TPayload>
{
    private readonly TaskScheduler scheduler;

    /// <summary>
    /// Creates a subscription that schedules callbacks on <see cref="TaskScheduler.Default"/>.
    /// </summary>
    /// <param name="actionReference">A reference to the asynchronous callback.</param>
    /// <param name="filterReference">A reference to the subscription filter.</param>
    /// <returns>A subscription configured for thread-pool scheduling.</returns>
    public static TaskEventSubscription<TPayload> CreateDefault(IDelegateReference actionReference, IDelegateReference filterReference)
    {
        return new TaskEventSubscription<TPayload>(actionReference, filterReference, TaskScheduler.Default);
    }

    /// <summary>
    /// Creates a subscription that schedules callbacks through a synchronization context.
    /// </summary>
    /// <param name="actionReference">A reference to the asynchronous callback.</param>
    /// <param name="filterReference">A reference to the subscription filter.</param>
    /// <param name="synchronizationContext">The synchronization context used for scheduling.</param>
    /// <returns>A subscription configured for synchronization-context scheduling.</returns>
    public static TaskEventSubscription<TPayload> CreateForSynchronizationContext(IDelegateReference actionReference, IDelegateReference filterReference, SynchronizationContext synchronizationContext)
    {
        return new TaskEventSubscription<TPayload>(actionReference, filterReference,TaskSchedulerHelper.FromSynchronizationContext(synchronizationContext));
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TaskEventSubscription{TPayload}"/>.
    /// </summary>
    /// <param name="actionReference">A reference to the asynchronous callback.</param>
    /// <param name="filterReference">A reference to the subscription filter.</param>
    /// <param name="scheduler">The scheduler used to invoke the callback.</param>
    private TaskEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference, TaskScheduler scheduler)
        : base(actionReference, filterReference)
    {
        this.scheduler = scheduler;
    }

    /// <summary>
    /// Schedules filter evaluation and invocation of the subscriber callback.
    /// </summary>
    /// <param name="payload">The payload supplied by the publisher.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes after the scheduler has invoked the callback when
    /// the filter accepts the payload.
    /// </returns>
    /// <remarks>
    /// The <see cref="ValueTask"/> returned by the callback itself is not awaited by this implementation.
    /// </remarks>
    public override async ValueTask InvokeAction(TPayload payload)
    {
        var action = Action;
        var filter = Filter;

        if (action != null)
        {
            if (filter != null)
            {
                await Task.Factory.StartNew(() =>
                {
                    if (filter(payload))
                    {
                        action.Invoke(payload);
                    }
                }, CancellationToken.None, TaskCreationOptions.None, scheduler);
            }
        }
    }
}
