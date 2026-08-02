using System.Buffers;
using System.Runtime.InteropServices;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Represents a publish/subscribe event with a strongly typed, allocation-optimized payload path.
/// </summary>
/// <typeparam name="TPayload">The type of payload delivered to subscribers.</typeparam>
/// <remarks>
/// Publication invokes strongly typed subscription methods directly, avoiding the object-array
/// execution strategies used by <see cref="PubSubEvent{TPayload}"/>. Each publication operates on
/// a stable snapshot of the subscribers observed at its start.
/// </remarks>
public class LightweightPubSubEvent<TPayload> : EventBase
{
    /// <summary>
    /// Subscribes a weakly referenced callback on the <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The callback invoked with the published payload.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public SubscriptionToken Subscribe(Action<TPayload> action)
    {
        return Subscribe(action, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Subscribes a weakly referenced, filtered callback on the
    /// <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The callback invoked with an accepted payload.</param>
    /// <param name="filter">
    /// The predicate that determines whether <paramref name="action"/> receives a payload.
    /// A <see langword="null"/> filter accepts every payload.
    /// </param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public virtual SubscriptionToken Subscribe(Action<TPayload> action, Predicate<TPayload> filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    /// <summary>
    /// Subscribes a weakly referenced callback using the specified thread option.
    /// </summary>
    /// <param name="action">The callback invoked with the published payload.</param>
    /// <param name="threadOption">The thread on which the callback is dispatched.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="threadOption"/> is <see cref="ThreadOption.UIThread"/> and no
    /// <see cref="EventBase.SynchronizationContext"/> is available.
    /// </exception>
    public SubscriptionToken Subscribe(Action<TPayload> action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false);
    }

    /// <summary>
    /// Subscribes a callback on the <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The callback invoked with the published payload.</param>
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
    public SubscriptionToken Subscribe(Action<TPayload> action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
    }

    /// <summary>
    /// Subscribes a callback using the specified thread option and reference strength.
    /// </summary>
    /// <param name="action">The callback invoked with the published payload.</param>
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
    public SubscriptionToken Subscribe(Action<TPayload> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    /// <summary>
    /// Subscribes a filtered callback using the specified thread option and reference strength.
    /// </summary>
    /// <param name="action">The callback invoked with an accepted payload.</param>
    /// <param name="threadOption">The thread on which the filter and callback are dispatched.</param>
    /// <param name="keepSubscriberReferenceAlive">
    /// <see langword="true"/> to keep strong references to the callback and filter targets;
    /// otherwise, <see langword="false"/> to use weak references.
    /// </param>
    /// <param name="filter">
    /// The predicate that determines whether <paramref name="action"/> receives a payload.
    /// A <see langword="null"/> filter accepts every payload.
    /// </param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="threadOption"/> is <see cref="ThreadOption.UIThread"/> and no
    /// <see cref="EventBase.SynchronizationContext"/> is available.
    /// </exception>
    /// <remarks>
    /// Strongly referenced callbacks and filters must be explicitly unsubscribed when the
    /// subscriber is disposed to avoid retaining their targets.
    /// </remarks>
    public virtual SubscriptionToken Subscribe(Action<TPayload> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<TPayload> filter)
    {
        IDelegateReference actionReference = new DelegateReference(action, keepSubscriberReferenceAlive);

        IDelegateReference filterReference;
        if (filter != null)
        {
            filterReference = new DelegateReference(filter, keepSubscriberReferenceAlive);
        }
        else
        {
            filterReference = new DelegateReference(new Predicate<TPayload>(delegate { return true; }), true);
        }

        EventSubscription<TPayload> subscription;
        switch (threadOption)
        {
            case ThreadOption.PublisherThread:
                subscription = new EventSubscription<TPayload>(actionReference, filterReference);
                break;
            case ThreadOption.BackgroundThread:
                subscription = new BackgroundEventSubscription<TPayload>(actionReference, filterReference);
                break;
            case ThreadOption.UIThread:
                if (SynchronizationContext == null)
                    throw new InvalidOperationException(Resources.EventAggregatorNotConstructedOnUIThread);
                subscription = new DispatcherEventSubscription<TPayload>(actionReference, filterReference, SynchronizationContext);
                break;
            default:
                subscription = new EventSubscription<TPayload>(actionReference, filterReference);
                break;
        }

        return InternalSubscribe(subscription);
    }

    /// <summary>
    /// Publishes a payload to a snapshot of the current subscribers.
    /// </summary>
    /// <param name="payload">The payload delivered to subscribers whose filters accept it.</param>
    public virtual void Publish(TPayload payload)
    {
        PublishImplementation(payload);
    }

    /// <inheritdoc/>
    protected override void InternalPublish(params object[] arguments)
    {
        TPayload argument = default;

        if (arguments != null && arguments.Length > 0 && arguments[0] != null)
        {
            argument = (TPayload)arguments[0];
        }

        PublishImplementation(argument);
    }

    /// <summary>
    /// Publishes the event through the allocation-optimized subscription path.
    /// </summary>
    protected void PublishImplementation(TPayload payload)
    {
        var activeSubscribers = LightweightPubSubEvent.PruneSubscribers<EventSubscription<TPayload>>((List<IEventSubscription>)Subscriptions);

        try
        {
            var subscribtions = activeSubscribers.Subscribtions;

            for (int i = 0; i < activeSubscribers.Count; i++)
            {
                EventSubscription<TPayload> subscriber = subscribtions[i] as EventSubscription<TPayload>;

                if (subscriber != null)
                {
                    subscriber.InvokeAction(payload);
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
    /// Adds an event subscription and assigns its unique subscription token.
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
    /// Removes the first subscriber matching <see cref="Action{TPayload}"/> from the subscribers' list.
    /// </summary>
    /// <param name="subscriber">The <see cref="Action{TPayload}"/> used when subscribing to the event.</param>
    public virtual void Unsubscribe(Action<TPayload> subscriber)
    {
        lock (Subscriptions)
        {
            IEventSubscription eventSubscription = Subscriptions.Cast<EventSubscription<TPayload>>().FirstOrDefault(evt => evt.Action == subscriber);
            if (eventSubscription != null)
            {
                Subscriptions.Remove(eventSubscription);
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if there is a subscriber matching <see cref="Action{TPayload}"/>.
    /// </summary>
    /// <param name="subscriber">The <see cref="Action{TPayload}"/> used when subscribing to the event.</param>
    /// <returns><see langword="true"/> if there is an <see cref="Action{TPayload}"/> that matches; otherwise <see langword="false"/>.</returns>
    public virtual bool Contains(Action<TPayload> subscriber)
    {
        IEventSubscription eventSubscription;
        lock (Subscriptions)
        {
            eventSubscription = Subscriptions.Cast<EventSubscription<TPayload>>().FirstOrDefault(evt => evt.Action == subscriber);
        }
        return eventSubscription != null;
    }
}
