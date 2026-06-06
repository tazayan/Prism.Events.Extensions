namespace Prism.Events.Extensions;

public static class TaskSchedulerHelper
{
    public static TaskScheduler FromSynchronizationContext(SynchronizationContext synchronizationContext)
    {
        SynchronizationContext current = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            return TaskScheduler.FromCurrentSynchronizationContext();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(current);
        }
    }
}
