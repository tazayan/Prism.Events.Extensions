using System.Buffers;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Represents a parameterless publish/subscribe event whose subscriber callbacks return
/// <see cref="ValueTask"/>.
/// </summary>
/// <remarks>
/// Subscribers are invoked sequentially from a stable snapshot. Use <see cref="Send"/> when the
/// publisher needs a completion handle for the publication operation.
/// </remarks>
public class AsyncPubSubEvent : EventBase
{
    /// <summary>
    /// Subscribes a weakly referenced asynchronous callback on the
    /// <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked when the event is published.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public SubscriptionToken Subscribe(Func<ValueTask> action)
    {
        return Subscribe(action, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Subscribes a weakly referenced asynchronous callback using the specified thread option.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked when the event is published.</param>
    /// <param name="threadOption">The thread on which the callback is dispatched.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="threadOption"/> is <see cref="ThreadOption.UIThread"/> and no
    /// <see cref="EventBase.SynchronizationContext"/> is available.
    /// </exception>
    public SubscriptionToken Subscribe(Func<ValueTask> action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false);
    }

    /// <summary>
    /// Subscribes an asynchronous callback on the <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked when the event is published.</param>
    /// <param name="keepSubscriberReferenceAlive">
    /// <see langword="true"/> to keep a strong reference to the callback target; otherwise,
    /// <see langword="false"/> to use a weak reference.
    /// </param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Strongly referenced callbacks must be explicitly unsubscribed when the subscriber is
    /// disposed to avoid retaining the subscriber.
    /// </remarks>
    public SubscriptionToken Subscribe(Func<ValueTask> action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
    }

    /// <summary>
    /// Subscribes an asynchronous callback using the specified thread option and reference strength.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked when the event is published.</param>
    /// <param name="threadOption">The thread on which the callback is dispatched.</param>
    /// <param name="keepSubscriberReferenceAlive">
    /// <see langword="true"/> to keep a strong reference to the callback target; otherwise,
    /// <see langword="false"/> to use a weak reference.
    /// </param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="threadOption"/> is <see cref="ThreadOption.UIThread"/> and no
    /// <see cref="EventBase.SynchronizationContext"/> is available.
    /// </exception>
    /// <remarks>
    /// Strongly referenced callbacks must be explicitly unsubscribed when the subscriber is
    /// disposed to avoid retaining the subscriber.
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
    /// Begins publishing the event to the current subscribers without returning a completion handle.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Send"/> to observe completion. Because this method is <see langword="async"/>
    /// <see langword="void"/>, exceptions raised after it returns cannot be observed through a task.
    /// </remarks>
    public async virtual void Publish()
    {
        await PublishImplementation();
    }

    /// <summary>
    /// Publishes the event and returns a handle for the asynchronous publication operation.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes after publisher-thread callbacks have completed and
    /// scheduler-backed callbacks have been invoked by their scheduler.
    /// </returns>
    /// <remarks>
    /// Use this method instead of <see cref="Publish"/> when the publisher needs to observe
    /// completion or exceptions. Scheduler-backed subscriptions currently do not await the
    /// <see cref="ValueTask"/> returned by their callbacks.
    /// </remarks>
    public virtual ValueTask Send()
    {
        return PublishImplementation();
    }

    /// <summary>
    /// Begins asynchronous publication through the <see cref="EventBase"/> compatibility path.
    /// </summary>
    /// <param name="arguments">Ignored because this event has no payload.</param>
    protected override async void InternalPublish(params object[] arguments)
    {
        await PublishImplementation();
    }

    /// <summary>
    /// Publishes the event sequentially to a snapshot of the active subscriptions.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the publication loop.</returns>
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

    /// <summary>
    /// Adds an asynchronous event subscription and assigns its unique subscription token.
    /// </summary>
    /// <param name="eventSubscription">The subscription to add.</param>
    /// <returns>The token assigned to <paramref name="eventSubscription"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="eventSubscription"/> is <see langword="null"/>.
    /// </exception>
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
    /// Removes the first subscriber whose callback matches <paramref name="subscriber"/>.
    /// </summary>
    /// <param name="subscriber">The asynchronous callback used when subscribing to the event.</param>
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
    /// Determines whether the event contains a callback matching <paramref name="subscriber"/>.
    /// </summary>
    /// <param name="subscriber">The asynchronous callback used when subscribing to the event.</param>
    /// <returns><see langword="true"/> when a matching callback exists; otherwise, <see langword="false"/>.</returns>
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
