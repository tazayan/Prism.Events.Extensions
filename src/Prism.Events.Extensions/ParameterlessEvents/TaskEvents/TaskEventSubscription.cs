namespace Prism.Events.Extensions;

/// <summary>
/// An <see cref="AsyncEventSubscription"/> that schedules callback invocation through a
/// <see cref="TaskScheduler"/>.
/// </summary>
internal class TaskEventSubscription : AsyncEventSubscription
{
    private readonly TaskScheduler scheduler;

    /// <summary>
    /// Creates a subscription that schedules callbacks on <see cref="TaskScheduler.Default"/>.
    /// </summary>
    /// <param name="actionReference">A reference to the asynchronous callback.</param>
    /// <returns>A subscription configured for thread-pool scheduling.</returns>
    public static TaskEventSubscription CreateDefault(IDelegateReference actionReference)
    {
        return new TaskEventSubscription(actionReference, TaskScheduler.Default);
    }

    /// <summary>
    /// Creates a subscription that schedules callbacks through a synchronization context.
    /// </summary>
    /// <param name="actionReference">A reference to the asynchronous callback.</param>
    /// <param name="synchronizationContext">The synchronization context used for scheduling.</param>
    /// <returns>A subscription configured for synchronization-context scheduling.</returns>
    public static TaskEventSubscription CreateForSynchronizationContext(IDelegateReference actionReference, SynchronizationContext synchronizationContext)
    {
        return new TaskEventSubscription(actionReference, TaskSchedulerHelper.FromSynchronizationContext(synchronizationContext));
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TaskEventSubscription"/>.
    /// </summary>
    /// <param name="actionReference">A reference to the asynchronous callback.</param>
    /// <param name="scheduler">The scheduler used to invoke the callback.</param>
    private TaskEventSubscription(IDelegateReference actionReference, TaskScheduler scheduler)
        : base(actionReference)
    {
        this.scheduler = scheduler;
    }

    /// <summary>
    /// Schedules invocation of the subscriber callback.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes after the scheduler has invoked the callback.
    /// </returns>
    /// <remarks>
    /// The <see cref="ValueTask"/> returned by the callback itself is not awaited by this implementation.
    /// </remarks>
    public override async ValueTask InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
            await Task.Factory.StartNew(() => action.Invoke(), default, TaskCreationOptions.None, scheduler);
        }
    }
}
