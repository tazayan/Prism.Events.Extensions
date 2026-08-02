using System.Buffers;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Defines a class that manages publication and subscription to events.
/// </summary>
public class AsyncPubSubEvent : EventBase
{
    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// <see cref="AsyncPubSubEvent"/> will maintain a <see cref="WeakReference"/> to the target of the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// The AsyncPubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Func<ValueTask> action)
    {
        return Subscribe(action, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// AsyncPubSubEvent will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is raised.</param>
    /// <param name="threadOption">Specifies on which thread to receive the delegate callback.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// The AsyncPubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Func<ValueTask> action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false);
    }

    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <param name="keepSubscriberReferenceAlive">When <see langword="true"/>, the <see cref="AsyncPubSubEvent"/> keeps a reference to the subscriber so it does not get garbage collected.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// If <paramref name="keepSubscriberReferenceAlive"/> is set to <see langword="false" />, <see cref="AsyncPubSubEvent"/> will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// If not using a WeakReference (<paramref name="keepSubscriberReferenceAlive"/> is <see langword="true" />), the user must explicitly call Unsubscribe for the event when disposing the subscriber in order to avoid memory leaks or unexpected behavior.
    /// <para/>
    /// The PubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Func<ValueTask> action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <param name="threadOption">Specifies on which thread to receive the delegate callback.</param>
    /// <param name="keepSubscriberReferenceAlive">When <see langword="true"/>, the <see cref="AsyncPubSubEvent"/> keeps a reference to the subscriber so it does not get garbage collected.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// If <paramref name="keepSubscriberReferenceAlive"/> is set to <see langword="false" />, <see cref="AsyncPubSubEvent"/> will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// If not using a WeakReference (<paramref name="keepSubscriberReferenceAlive"/> is <see langword="true" />), the user must explicitly call Unsubscribe for the event when disposing the subscriber in order to avoid memory leaks or unexpected behavior.
    /// <para/>
    /// The PubSubEvent collection is thread-safe.
    /// </remarks>
    public virtual SubscriptionToken Subscribe(Func<ValueTask> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        IDelegateReference actionReference = new DelegateReference(action, keepSubscriberReferenceAlive);

        AsyncEventSubscription subscription;
        switch (threadOption)
        {
            case ThreadOption.PublisherThread:
                subscription = new AsyncEventSubscription(actionReference);
                break;
            case ThreadOption.BackgroundThread:
                subscription = TaskEventSubscription.CreateDefault(actionReference);
                break;
            case ThreadOption.UIThread:
                if (SynchronizationContext == null)
                    throw new InvalidOperationException(Resources.EventAggregatorNotConstructedOnUIThread);
                subscription = TaskEventSubscription.CreateForSynchronizationContext(actionReference, SynchronizationContext);
                break;
            default:
                subscription = new AsyncEventSubscription(actionReference);
                break;
        }

        return InternalSubscribe(subscription);
    }

    /// <summary>
    /// Asynchronusly published the event to all subscribers.
    /// the same way as <see cref="EventBase.Publish"/> does.
    /// </summary>
    public async virtual void Publish()
    {
        await PublishImplementation();
    }

    /// <summary>
    /// Asynchronusly published the event to all subscribers and returns a task representing the asynchronous operation which completes when the all events handler completes.
    /// This is new API and should be used instead of <see cref="Publish"/> when publisher needs to know when the operation is complete.
    /// </summary>
    /// <returns></returns>
    public virtual ValueTask Send()
    {
        return PublishImplementation();
    }

    /// <inheritdoc/>
    protected override async void InternalPublish(params object[] arguments)
    {
        await PublishImplementation();
    }

    /// <inheritdoc/>
    private async ValueTask PublishImplementation()
    {
        var activeSubscribers = LightweightPubSubEvent.PruneSubscribers<AsyncEventSubscription>((List<IEventSubscription>)Subscriptions);

        try
        {
            var subscribtions = activeSubscribers.Subscribtions;

            for (int i = 0; i < activeSubscribers.Count; i++)
            {
                AsyncEventSubscription subscriber = subscribtions[i] as AsyncEventSubscription;

                if (subscriber != null)
                {
                    await subscriber.InvokeAction();
                }
            }
        }
        finally
        {
            if (activeSubscribers.Count > 0)
            {
                ArrayPool<IEventSubscription>.Shared.Return(activeSubscribers.Subscribtions, clearArray: true);
            }
        }
    }

    /// <inheritdoc/>
    protected override SubscriptionToken InternalSubscribe(IEventSubscription eventSubscription)
    {
        if (eventSubscription == null) throw new ArgumentNullException(nameof(eventSubscription));

        eventSubscription.SubscriptionToken = new SubscriptionToken(Unsubscribe);

        lock (Subscriptions)
        {
            Subscriptions.Add(eventSubscription);
        }
        return eventSubscription.SubscriptionToken;
    }

    /// <summary>
    /// Removes the first subscriber matching <see cref="Action"/> from the subscribers' list.
    /// </summary>
    /// <param name="subscriber">The <see cref="Action"/> used when subscribing to the event.</param>
    public virtual void Unsubscribe(Func<ValueTask> subscriber)
    {
        lock (Subscriptions)
        {
            IEventSubscription eventSubscription = Subscriptions.Cast<AsyncEventSubscription>().FirstOrDefault(evt => evt.Action == subscriber);
            if (eventSubscription != null)
            {
                Subscriptions.Remove(eventSubscription);
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if there is a subscriber matching <see cref="Action"/>.
    /// </summary>
    /// <param name="subscriber">The <see cref="Action"/> used when subscribing to the event.</param>
    /// <returns><see langword="true"/> if there is an <see cref="Action"/> that matches; otherwise <see langword="false"/>.</returns>
    public virtual bool Contains(Func<ValueTask> subscriber)
    {
        IEventSubscription eventSubscription;
        lock (Subscriptions)
        {
            eventSubscription = Subscriptions.Cast<AsyncEventSubscription>().FirstOrDefault(evt => evt.Action == subscriber);
        }
        return eventSubscription != null;
    }
}
