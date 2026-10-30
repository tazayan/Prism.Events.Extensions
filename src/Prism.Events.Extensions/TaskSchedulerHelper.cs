namespace Prism.Events.Extensions;

/// <summary>
/// Provides helper methods for working with <see cref="TaskScheduler"/> instances.
/// </summary>
public static class TaskSchedulerHelper
{
    /// <summary>
    /// Creates a <see cref="TaskScheduler"/> that queues tasks to the specified
    /// <see cref="SynchronizationContext"/>.
    /// </summary>
    /// <param name="synchronizationContext">
    /// The synchronization context to which the returned scheduler posts its tasks.
    /// </param>
    /// <returns>A task scheduler that runs tasks through <paramref name="synchronizationContext"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="synchronizationContext"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <see cref="TaskScheduler.FromCurrentSynchronizationContext"/> works only with the calling
    /// thread's current context. This method briefly installs <paramref name="synchronizationContext"/>
    /// as the current context of the calling thread, creates the scheduler, and then restores the
    /// previous context, even if an exception is thrown.
    /// </remarks>
    public static TaskScheduler FromSynchronizationContext(SynchronizationContext synchronizationContext)
    {
        ArgumentNullException.ThrowIfNull(synchronizationContext);

        SynchronizationContext previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            return TaskScheduler.FromCurrentSynchronizationContext();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
