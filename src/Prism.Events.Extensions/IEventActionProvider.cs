namespace Prism.Events.Extensions;

/// <summary>
/// Provides the liveness state of the delegates required by an event subscription.
/// </summary>
internal interface IEventActionProvider
{
    /// <summary>
    /// Determines whether all delegates required to invoke the subscription are still alive.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the subscription can still be invoked; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    bool IsSubscriptionAlive();
}
