namespace Prism.Events.Extensions;

internal class TaskEventSubscription<TPayload> : AsyncEventSubscription<TPayload>
{
    private readonly TaskScheduler scheduler;

    public static TaskEventSubscription<TPayload> CreateDefault(IDelegateReference actionReference, IDelegateReference filterReference)
    {
        return new TaskEventSubscription<TPayload>(actionReference, filterReference, TaskScheduler.Default);
    }

    public static TaskEventSubscription<TPayload> CreateForSynchronizationContext(IDelegateReference actionReference, IDelegateReference filterReference, SynchronizationContext synchronizationContext)
    {
        return new TaskEventSubscription<TPayload>(actionReference, filterReference,TaskSchedulerHelper.FromSynchronizationContext(synchronizationContext));
    }

    private TaskEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference, TaskScheduler scheduler)
        : base(actionReference, filterReference)
    {
        this.scheduler = scheduler;
    }

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
