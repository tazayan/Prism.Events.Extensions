namespace Prism.Events.Extensions;

/// <summary>
/// Provides helpers for creating task schedulers from synchronization contexts.
/// </summary>
public static class TaskSchedulerHelper
{
    /// <summary>
    /// Creates a <see cref="TaskScheduler"/> that schedules work through the specified
    /// <see cref="SynchronizationContext"/>.
    /// </summary>
    /// <param name="synchronizationContext">
    /// The synchronization context through which the returned scheduler dispatches work.
    /// </param>
    /// <returns>A task scheduler associated with <paramref name="synchronizationContext"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// A task scheduler cannot be created from <paramref name="synchronizationContext"/>.
    /// </exception>
    /// <remarks>
    /// The method temporarily installs <paramref name="synchronizationContext"/> as the current
    /// context on the calling thread and restores the previous context before returning.
    /// </remarks>
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
