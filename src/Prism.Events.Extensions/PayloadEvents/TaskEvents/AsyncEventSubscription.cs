using System.Globalization;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Represents a filtered subscription whose payload callback returns a <see cref="ValueTask"/>.
/// </summary>
/// <typeparam name="TPayload">The type of payload delivered to the subscriber.</typeparam>
class AsyncEventSubscription<TPayload> : IEventSubscription, IEventActionProvider
{
    private readonly IDelegateReference actionReference;
    private readonly IDelegateReference filterReference;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncEventSubscription{TPayload}"/>.
    /// </summary>
    /// <param name="actionReference">
    /// A reference to a <see cref="Func{T, TResult}"/> accepting <typeparamref name="TPayload"/> and
    /// returning a <see cref="ValueTask"/>.
    /// </param>
    /// <param name="filterReference">A reference to a <see cref="Predicate{TPayload}"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="actionReference"/> or <paramref name="filterReference"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A delegate reference does not contain the required delegate type.
    /// </exception>
    public AsyncEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference)
    {
        if (actionReference == null)
            throw new ArgumentNullException(nameof(actionReference));
        if (!(actionReference.Target is Func<TPayload, ValueTask>))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDelegateRerefenceTypeException, typeof(Action).FullName), nameof(actionReference));

        if (filterReference == null)
            throw new ArgumentNullException(nameof(filterReference));
        if (!(filterReference.Target is Predicate<TPayload>))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDelegateRerefenceTypeException, typeof(Predicate<TPayload>).FullName), nameof(filterReference));

        this.actionReference = actionReference;
        this.filterReference = filterReference;
    }


    /// <summary>
    /// Gets the asynchronous callback referenced by the callback's <see cref="IDelegateReference"/>.
    /// </summary>
    /// <value>
    /// A callback accepting <typeparamref name="TPayload"/> and returning a <see cref="ValueTask"/>,
    /// or <see langword="null"/> if the weakly referenced target is no longer alive.
    /// </value>
    public Func<TPayload, ValueTask> Action
    {
        get { return (Func<TPayload, ValueTask>)actionReference.Target; }
    }

    /// <summary>
    /// Gets the <see cref="Predicate{TPayload}"/> referenced by the filter's
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
    /// A strategy that extracts the first argument as <typeparamref name="TPayload"/> and initiates
    /// <see cref="InvokeAction(TPayload)"/>, or <see langword="null"/> when the callback or filter is
    /// no longer alive.
    /// </returns>
    /// <remarks>
    /// The compatibility strategy cannot await the <see cref="ValueTask"/> returned by
    /// <see cref="InvokeAction(TPayload)"/>. Normal <see cref="AsyncPubSubEvent{TPayload}"/>
    /// publication invokes the strongly typed method directly.
    /// </remarks>
    Action<object[]> IEventSubscription.GetExecutionStrategy()
    {
        Func<TPayload, ValueTask> action = Action;
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
    /// Evaluates the subscription filter and invokes the asynchronous callback when the filter
    /// returns <see langword="true"/>.
    /// </summary>
    /// <param name="payload">The payload supplied by the publisher.</param>
    /// <returns>
    /// The callback's <see cref="ValueTask"/>, or <see cref="ValueTask.CompletedTask"/> when the
    /// callback or filter is unavailable or the filter rejects the payload.
    /// </returns>
    public virtual ValueTask InvokeAction(TPayload payload)
    {
        var action = Action;
        var filter = Filter;

        if (action != null)
        {
            if (filter != null && filter(payload))
            {
                return action(payload);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Determines whether the subscriber callback is still alive.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when <see cref="Action"/> is available; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool IsActionAlive()
    {
        return Action != null;
    }
}
