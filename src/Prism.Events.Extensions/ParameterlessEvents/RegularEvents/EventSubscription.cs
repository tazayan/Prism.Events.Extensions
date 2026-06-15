using System.Globalization;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Represents a subscription to a parameterless event, implementing <see cref="IEventSubscription"/>
/// with an allocation-optimized publish path.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the standard Prism <see cref=" Prism.Events.EventSubscription"/> contract, this type is designed so that
/// <see cref="LightweightPubSubEvent"/> casts each subscription to <see cref="EventSubscription"/> and
/// calls <see cref="InvokeAction"/> directly during publish, bypassing
/// <see cref="IEventSubscription.GetExecutionStrategy"/>.
/// </para>
/// <para>
/// This avoids the per-publish allocation of an <see cref="Action{T}"/> wrapper delegate and an
/// <c>object[]</c> arguments array that the standard <see cref="IEventSubscription"/> dispatch flow
/// would otherwise produce.
/// </para>
/// </remarks>
class EventSubscription : IEventSubscription
{
    private readonly IDelegateReference actionReference;

    /// <summary>
    /// Initializes a new instance of <see cref="EventSubscription"/> with the specified delegate reference.
    /// </summary>
    /// <param name="actionReference">
    /// A reference to the subscriber <see cref="Action"/> delegate. Must not be <see langword="null"/>
    /// and its <see cref="IDelegateReference.Target"/> must be of type <see cref="Action"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actionReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="actionReference"/> does not reference an <see cref="Action"/> delegate.
    /// </exception>
    public EventSubscription(IDelegateReference actionReference)
    {
        if (actionReference == null)
            throw new ArgumentNullException(nameof(actionReference));
        if (!(actionReference.Target is Action))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDelegateRerefenceTypeException, typeof(Action).FullName), nameof(actionReference));

        this.actionReference = actionReference;
    }

    /// <summary>
    /// Gets the target <see cref="Action"/> delegate referenced by the underlying <see cref="IDelegateReference"/>.
    /// </summary>
    /// <value>
    /// The subscriber <see cref="Action"/>, or <see langword="null"/> if the weak-reference target
    /// has been garbage collected.
    /// </value>
    public Action Action
    {
        get { return (Action)actionReference.Target; }
    }

    /// <summary>
    /// Gets or sets the <see cref="SubscriptionToken"/> that uniquely identifies this subscription.
    /// </summary>
    /// <value>A token that identifies this <see cref="IEventSubscription"/>.</value>
    public SubscriptionToken SubscriptionToken { get; set; }

    /// <summary>
    /// Returns an <see cref="Action{T}"/> execution strategy for compatibility with the
    /// <see cref="IEventSubscription"/> interface contract.
    /// </summary>
    /// <returns>
    /// An <see cref="Action{T}"/> that ignores its <c>object[]</c> arguments and delegates to
    /// <see cref="InvokeAction"/>, or <see langword="null"/> if the subscriber delegate is no longer alive.
    /// </returns>
    /// <remarks>
    /// This explicit interface implementation exists solely for <see cref="IEventSubscription"/> compatibility.
    /// It is <b>not</b> used by <see cref="LightweightPubSubEvent"/>, which instead casts subscriptions
    /// directly to <see cref="EventSubscription"/> and calls <see cref="InvokeAction"/> to avoid
    /// allocating a wrapping <see cref="Action{T}"/> delegate and an <c>object[]</c> parameters array
    /// on every publish.
    /// </remarks>
    Action<object[]> IEventSubscription.GetExecutionStrategy()
    {
        Action action = Action;

        if (action != null)
        {
            return arguments =>
            {
                InvokeAction();
            };
        }

        return null;
    }

    /// <summary>
    /// Invokes the subscriber's <see cref="Action"/> delegate on the calling (publisher's) thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="LightweightPubSubEvent.InternalPublish"/> calls this method directly by casting each
    /// <see cref="IEventSubscription"/> to <see cref="EventSubscription"/>, bypassing the
    /// <see cref="IEventSubscription.GetExecutionStrategy"/> delegate chain. This eliminates the
    /// per-publish allocation of an <see cref="Action{T}"/> wrapper and an <c>object[]</c> arguments
    /// array that the standard Prism event dispatch flow would otherwise produce.
    /// </para>
    /// <para>
    /// This method is <see langword="virtual"/> so that derived classes can override the threading
    /// behavior; for example, <see cref="BackgroundEventSubscription"/> dispatches the invocation to
    /// a thread-pool thread via <see cref="Task.Run(Action)"/>.
    /// </para>
    /// <para>
    /// If the subscriber delegate is no longer alive (i.e., <see cref="Action"/> returns
    /// <see langword="null"/>), the invocation is silently skipped.
    /// </para>
    /// </remarks>
    public virtual void InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
            action();
        }
    }
}
