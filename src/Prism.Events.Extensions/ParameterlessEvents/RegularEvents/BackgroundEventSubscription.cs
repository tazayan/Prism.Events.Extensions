namespace Prism.Events.Extensions;

/// <summary>
/// An <see cref="EventSubscription"/> that dispatches the subscriber's <see cref="System.Action"/>
/// to a thread-pool thread, corresponding to <see cref="ThreadOption.BackgroundThread"/>.
/// </summary>
internal class BackgroundEventSubscription : EventSubscription
{
    /// <summary>
    /// Initializes a new instance of <see cref="BackgroundEventSubscription"/> with the specified delegate reference.
    /// </summary>
    /// <param name="actionReference">
    /// A reference to the subscriber <see cref="System.Action"/> delegate.
    /// Passed directly to the <see cref="EventSubscription"/> base constructor.
    /// </param>
    public BackgroundEventSubscription(IDelegateReference actionReference) : base(actionReference)
    {
    }

    /// <summary>
    /// Dispatches the subscriber's <see cref="System.Action"/> delegate to a thread-pool thread
    /// via <see cref="Task.Run(System.Action)"/>, decoupling execution from the publisher's thread.
    /// </summary>
    /// <remarks>
    /// If the subscriber delegate is no longer alive (i.e., <see cref="EventSubscription.Action"/>
    /// returns <see langword="null"/>), the dispatch is silently skipped.
    /// </remarks>
    public override void InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
            _ = Task.Run(action);
        }
    }
}
