namespace Prism.Events.Extensions;

internal class DispatcherEventSubscription<TPayload> : EventSubscription<TPayload>
{
    private readonly SynchronizationContext synchronizationContext;

    public DispatcherEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference, SynchronizationContext synchronizationContext)
        : base(actionReference, filterReference)
    {
        this.synchronizationContext = synchronizationContext;
    }

    public override void InvokeAction(TPayload payload)
    {
        var action = Action;
        var filter = Filter;

        if (action != null)
        {
            synchronizationContext.Post((o) =>
            {
                if (filter != null && filter(payload))
                {
                    action(payload);
                }
            }, null);
        }
    }
}
