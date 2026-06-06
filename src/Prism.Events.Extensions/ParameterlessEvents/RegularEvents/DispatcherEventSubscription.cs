namespace Prism.Events.Extensions;

internal class DispatcherEventSubscription : EventSubscription
{
    private readonly SynchronizationContext synchronizationContext;

    public DispatcherEventSubscription(IDelegateReference actionReference, SynchronizationContext synchronizationContext)
        : base(actionReference)
    {
        this.synchronizationContext = synchronizationContext;
    }

    public override void InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
            synchronizationContext.Post((o) => action(), null);
        }
    }
}
