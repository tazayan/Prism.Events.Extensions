namespace Prism.Events.Extensions;

internal class TaskEventSubscription : AsyncEventSubscription
{
    private readonly TaskScheduler scheduler;

    public static TaskEventSubscription CreateDefault(IDelegateReference actionReference)
    {
        return new TaskEventSubscription(actionReference, TaskScheduler.Default);
    }

    public static TaskEventSubscription CreateForSynchronizationContext(IDelegateReference actionReference, SynchronizationContext synchronizationContext)
    {
        return new TaskEventSubscription(actionReference, TaskSchedulerHelper.FromSynchronizationContext(synchronizationContext));
    }

    private TaskEventSubscription(IDelegateReference actionReference, TaskScheduler scheduler)
        : base(actionReference)
    {
        this.scheduler = scheduler;
    }

    public override async ValueTask InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
            await Task.Factory.StartNew(() => action.Invoke(), default, TaskCreationOptions.None, scheduler);
        }
    }
}
