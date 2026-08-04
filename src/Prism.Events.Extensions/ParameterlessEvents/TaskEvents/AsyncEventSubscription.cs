using System.Globalization;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Represents a subscription whose parameterless callback returns a <see cref="ValueTask"/>.
/// </summary>
class AsyncEventSubscription : IEventSubscription, IEventActionProvider
{
    private readonly IDelegateReference actionReference;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncEventSubscription"/>.
    /// </summary>
    /// <param name="actionReference">A reference to a <see cref="Func{TResult}"/> returning a <see cref="ValueTask"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="actionReference"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="actionReference"/> does not contain a <see cref="Func{TResult}"/> returning a
    /// <see cref="ValueTask"/>.
    /// </exception>
    public AsyncEventSubscription(IDelegateReference actionReference)
    {
        if (actionReference == null)
            throw new ArgumentNullException(nameof(actionReference));
        if (!(actionReference.Target is Func<ValueTask>))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDelegateRerefenceTypeException, typeof(Action).FullName), nameof(actionReference));

        this.actionReference = actionReference;
    }


    /// <summary>
    /// Gets the asynchronous callback referenced by the <see cref="IDelegateReference"/>.
    /// </summary>
    /// <value>
    /// A parameterless <see cref="Func{TResult}"/> returning a <see cref="ValueTask"/>, or
    /// <see langword="null"/> if the weakly referenced target is no longer alive.
    /// </value>
    public Func<ValueTask> Action
    {
        get { return (Func<ValueTask>)actionReference.Target; }
    }

    /// <summary>
    /// Gets or sets a <see cref="Prism.Events.SubscriptionToken"/> that identifies this subscription.
    /// </summary>
    /// <value>A token that identifies this <see cref="IEventSubscription"/>.</value>
    public SubscriptionToken SubscriptionToken { get; set; }

    /// <summary>
    /// Gets an object-array execution strategy for <see cref="IEventSubscription"/> compatibility.
    /// </summary>
    /// <returns>
    /// A strategy that starts the asynchronous callback, or <see langword="null"/> when the
    /// weakly referenced callback is no longer alive.
    /// </returns>
    /// <remarks>
    /// The compatibility strategy cannot await the <see cref="ValueTask"/> returned by the callback.
    /// Normal <see cref="AsyncPubSubEvent"/> publication invokes <see cref="InvokeAction"/> directly.
    /// </remarks>
    Action<object[]> IEventSubscription.GetExecutionStrategy()
    {
        Func<ValueTask> action = Action;

        if (action != null)
        {
            return arguments =>
            {
                _ = InvokeAction();
            };
        }

        return null;
    }

    /// <summary>
    /// Invokes the callback and returns its asynchronous operation.
    /// </summary>
    /// <returns>
    /// The callback's <see cref="ValueTask"/>, or <see cref="ValueTask.CompletedTask"/> when the
    /// weakly referenced callback is no longer alive.
    /// </returns>
    public virtual ValueTask InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
           return action();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Determines whether the subscription can still be invoked.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when <see cref="Action"/> is available; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool IsSubscriptionAlive()
    {
        return Action != null;
    }
}
