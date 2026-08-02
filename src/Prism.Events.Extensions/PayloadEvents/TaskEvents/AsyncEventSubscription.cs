using System.Globalization;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

class AsyncEventSubscription<TPayload> : IEventSubscription, IEventActionProvider
{
    private readonly IDelegateReference actionReference;
    private readonly IDelegateReference filterReference;

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
    /// Gets the target <see cref="System.Action{T}"/> that is referenced by the <see cref="IDelegateReference"/>.
    /// </summary>
    /// <value>An <see cref="System.Action{T}"/> or <see langword="null" /> if the referenced target is not alive.</value>
    public Func<TPayload, ValueTask> Action
    {
        get { return (Func<TPayload, ValueTask>)actionReference.Target; }
    }

    /// <summary>
    /// Gets the target <see cref="Predicate{T}"/> that is referenced by the <see cref="IDelegateReference"/>.
    /// </summary>
    /// <value>An <see cref="Predicate{T}"/> or <see langword="null" /> if the referenced target is not alive.</value>
    public Predicate<TPayload> Filter
    {
        get { return (Predicate<TPayload>)filterReference.Target; }
    }

    /// <summary>
    /// Gets or sets a <see cref="SubscriptionToken"/> that identifies this <see cref="IEventSubscription"/>.
    /// </summary>
    /// <value>A token that identifies this <see cref="IEventSubscription"/>.</value>
    public SubscriptionToken SubscriptionToken { get; set; }

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

    public bool IsActionAlive()
    {
        return Action != null;
    }
}
