using System.Globalization;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

class EventSubscription : IEventSubscription
{
    private readonly IDelegateReference actionReference;

    public EventSubscription(IDelegateReference actionReference)
    {
        if (actionReference == null)
            throw new ArgumentNullException(nameof(actionReference));
        if (!(actionReference.Target is Action))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDelegateRerefenceTypeException, typeof(Action).FullName), nameof(actionReference));

        this.actionReference = actionReference;
    }

    public Action Action
    {
        get { return (Action)actionReference.Target; }
    }

    public SubscriptionToken SubscriptionToken { get; set; }

    Action<object[]> IEventSubscription.GetExecutionStrategy()
    {
        return null;
    }

    public virtual void InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
            action();
        }
    }
}
