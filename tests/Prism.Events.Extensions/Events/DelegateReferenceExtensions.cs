namespace Prism.Events.Extensions;

public static class DelegateReferenceExtensions
{
    public static IDelegateReference ToDelegateReference<T>(this Action<T> action, bool keepSubscriberReferenceAlive)
    {
        Func<T, ValueTask> wrapper = arg =>
        {
            action(arg);
            return ValueTask.CompletedTask;
        };
        return new DelegateReference(wrapper, keepSubscriberReferenceAlive);
    }

    public static IDelegateReference ToDelegateReference<T>(this Func<T, Task> action, bool keepSubscriberReferenceAlive)
    {
        Func<T, ValueTask> wrapper = arg => new ValueTask(action(arg));
        return new DelegateReference(wrapper, keepSubscriberReferenceAlive);
    }

    public static IDelegateReference ToDelegateReference<T>(this Func<T, ValueTask> action, bool keepSubscriberReferenceAlive)
    {
        return new DelegateReference(action, keepSubscriberReferenceAlive);
    }
}
