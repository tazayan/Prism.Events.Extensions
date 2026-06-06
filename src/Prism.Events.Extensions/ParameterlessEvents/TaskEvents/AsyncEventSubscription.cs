using System.Globalization;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

class AsyncEventSubscription : IEventSubscription
{
    private readonly IDelegateReference actionReference;

    public AsyncEventSubscription(IDelegateReference actionReference)
    {
        if (actionReference == null)
            throw new ArgumentNullException(nameof(actionReference));
        if (!(actionReference.Target is Func<ValueTask>))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDelegateRerefenceTypeException, typeof(Action).FullName), nameof(actionReference));

        this.actionReference = actionReference;
    }


    /// <summary>
    /// Gets the target <see cref="System.Action{T}"/> that is referenced by the <see cref="IDelegateReference"/>.
    /// </summary>
    /// <value>An <see cref="System.Action{T}"/> or <see langword="null" /> if the referenced target is not alive.</value>
    public Func<ValueTask> Action
    {
        get { return (Func<ValueTask>)actionReference.Target; }
    }

    /// <summary>
    /// Gets or sets a <see cref="SubscriptionToken"/> that identifies this <see cref="IEventSubscription"/>.
    /// </summary>
    /// <value>A token that identifies this <see cref="IEventSubscription"/>.</value>
    public SubscriptionToken SubscriptionToken { get; set; }

    Action<object[]> IEventSubscription.GetExecutionStrategy()
    {
        return null;
    }

    public virtual ValueTask InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
           return action();
        }

        return ValueTask.CompletedTask;
    }
}
