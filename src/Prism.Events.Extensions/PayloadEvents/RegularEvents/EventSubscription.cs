using System.Globalization;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Represents a subscription to an event carrying a payload, with a strongly typed callback and
/// filter path used by <see cref="LightweightPubSubEvent{TPayload}"/>.
/// </summary>
/// <typeparam name="TPayload">The type of payload delivered to the subscriber.</typeparam>
class EventSubscription<TPayload> : IEventSubscription, IEventActionProvider
{
    private readonly IDelegateReference actionReference;
    private readonly IDelegateReference filterReference;

    /// <summary>
    /// Initializes a new instance of <see cref="EventSubscription{TPayload}"/>.
    /// </summary>
    /// <param name="actionReference">
    /// A reference to an <see cref="Action{TPayload}"/> subscriber callback.
    /// </param>
    /// <param name="filterReference">
    /// A reference to a <see cref="Predicate{TPayload}"/> that determines whether the callback is invoked.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="actionReference"/> or <paramref name="filterReference"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A delegate reference does not contain the required delegate type.
    /// </exception>
    public EventSubscription(IDelegateReference actionReference, IDelegateReference filterReference)
    {
        if (actionReference == null)
            throw new ArgumentNullException(nameof(actionReference));
        if (!(actionReference.Target is Action<TPayload>))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDelegateRerefenceTypeException, typeof(Action).FullName), nameof(actionReference));

        if (filterReference == null)
            throw new ArgumentNullException(nameof(filterReference));
        if (!(filterReference.Target is Predicate<TPayload>))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDelegateRerefenceTypeException, typeof(Predicate<TPayload>).FullName), nameof(filterReference));

        this.actionReference = actionReference;
        this.filterReference = filterReference;
    }


    /// <summary>
    /// Gets the target <see cref="Action{TPayload}"/> referenced by the callback's
    /// <see cref="IDelegateReference"/>.
    /// </summary>
    /// <value>
    /// The subscriber callback, or <see langword="null"/> if its weakly referenced target is no longer alive.
    /// </value>
    public Action<TPayload> Action
    {
        get { return (Action<TPayload>)actionReference.Target; }
    }

    /// <summary>
    /// Gets the target <see cref="Predicate{TPayload}"/> referenced by the filter's
    /// <see cref="IDelegateReference"/>.
    /// </summary>
    /// <value>
    /// The subscription filter, or <see langword="null"/> if its weakly referenced target is no longer alive.
    /// </value>
    public Predicate<TPayload> Filter
    {
        get { return (Predicate<TPayload>)filterReference.Target; }
    }

    /// <summary>
    /// Gets or sets a <see cref="Prism.Events.SubscriptionToken"/> that identifies this subscription.
    /// </summary>
    /// <value>A token that identifies this <see cref="IEventSubscription"/>.</value>
    public SubscriptionToken SubscriptionToken { get; set; }

    /// <summary>
    /// Returns an object-array execution strategy for compatibility with
    /// <see cref="IEventSubscription"/>.
    /// </summary>
    /// <returns>
    /// A strategy that extracts the first argument as <typeparamref name="TPayload"/> and invokes
    /// <see cref="InvokeAction(TPayload)"/>, or <see langword="null"/> when the callback or filter is
    /// no longer alive.
    /// </returns>
    /// <remarks>
    /// <see cref="LightweightPubSubEvent{TPayload}.Publish(TPayload)"/> does not use this compatibility
    /// path; it invokes the strongly typed method directly.
    /// </remarks>
    Action<object[]> IEventSubscription.GetExecutionStrategy()
    {
        Action<TPayload> action = Action;
        Predicate<TPayload> filter = Filter;
        if (action != null && filter != null)
        {
            return arguments =>
            {
                TPayload argument = default(TPayload);
                if (arguments != null && arguments.Length > 0 && arguments[0] != null)
                {
                    argument = (TPayload)arguments[0];
                }

                InvokeAction(argument);
            };
        }
        return null;
    }

    /// <summary>
    /// Evaluates the subscription filter and synchronously invokes the callback when the filter
    /// returns <see langword="true"/>.
    /// </summary>
    /// <param name="payload">The payload supplied by the publisher.</param>
    public virtual void InvokeAction(TPayload payload)
    {
        var action = Action;
        var filter = Filter;

        if (action != null)
        {
            if (filter != null && filter(payload))
            {
                action(payload);
            }
        }
    }

    /// <summary>
    /// Determines whether the callback and filter required by the subscription are still alive.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when both <see cref="Action"/> and <see cref="Filter"/> are available;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsSubscriptionAlive()
    {
        return Action != null && Filter != null;
    }
}
