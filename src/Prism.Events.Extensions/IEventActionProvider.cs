namespace Prism.Events.Extensions;

/// <summary>
/// Provides the liveness state of the callback associated with an event subscription.
/// </summary>
internal interface IEventActionProvider
{
    /// <summary>
    /// Determines whether the subscription's callback target is still alive.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the callback can still be obtained; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    bool IsActionAlive();
}
