namespace Prism.Events.Extensions;

internal class BackgroundEventSubscription<TPayload> : EventSubscription<TPayload>
{
    public BackgroundEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference) :
        base(actionReference, filterReference)
    {

    }

    public override void InvokeAction(TPayload payload)
    {
        var action = Action;
        var filter = Filter;

        if (action != null)
        {
            Task.Run(() =>
            {
                if (filter != null && filter(payload))
                {
                    action(payload);
                }
            });
        }
    }
}
