namespace Prism.Events.Extensions;

internal class BackgroundEventSubscription : EventSubscription
{
    public BackgroundEventSubscription(IDelegateReference actionReference) : base(actionReference)
    {
    }

    public override void InvokeAction()
    {
        var action = Action;

        if (action != null)
        {
            Task.Run(action);
        }
    }
}
