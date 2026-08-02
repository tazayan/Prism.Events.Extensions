using System.Buffers;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Represents a filtered publish/subscribe event whose payload callbacks return
/// <see cref="ValueTask"/>.
/// </summary>
/// <typeparam name="TPayload">The type of payload delivered to subscribers.</typeparam>
/// <remarks>
/// Subscribers are invoked sequentially from a stable snapshot. Use <see cref="Send(TPayload)"/>
/// when the publisher needs a completion handle for the publication operation.
/// </remarks>
public class AsyncPubSubEvent<TPayload> : EventBase
{
    /// <summary>
    /// Subscribes a weakly referenced asynchronous callback on the
    /// <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked with the published payload.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public SubscriptionToken Subscribe(Func<TPayload, ValueTask> action)
    {
        return Subscribe(action, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Subscribes a weakly referenced, filtered asynchronous callback on the
    /// <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked with an accepted payload.</param>
    /// <param name="filter">
    /// The predicate that determines whether <paramref name="action"/> receives a payload.
    /// A <see langword="null"/> filter accepts every payload.
    /// </param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public virtual SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, Predicate<TPayload> filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    /// <summary>
    /// Subscribes a weakly referenced asynchronous callback using the specified thread option.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked with the published payload.</param>
    /// <param name="threadOption">The thread on which the callback is dispatched.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="threadOption"/> is <see cref="ThreadOption.UIThread"/> and no
    /// <see cref="EventBase.SynchronizationContext"/> is available.
    /// </exception>
    public SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false);
    }

    /// <summary>
    /// Subscribes an asynchronous callback on the <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked with the published payload.</param>
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
    public SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
    }

    /// <summary>
    /// Subscribes an asynchronous callback using the specified thread option and reference strength.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked with the published payload.</param>
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
    public SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    /// <summary>
    /// Subscribes a filtered asynchronous callback using the specified thread option and reference strength.
    /// </summary>
    /// <param name="action">The asynchronous callback invoked with an accepted payload.</param>
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
    public virtual SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<TPayload> filter)
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

        AsyncEventSubscription<TPayload> subscription;
        switch (threadOption)
        {
            case ThreadOption.PublisherThread:
                subscription = new AsyncEventSubscription<TPayload>(actionReference, filterReference);
                break;
            case ThreadOption.BackgroundThread:
                subscription = TaskEventSubscription<TPayload>.CreateDefault(actionReference, filterReference);
                break;
            case ThreadOption.UIThread:
                if (SynchronizationContext == null)
                    throw new InvalidOperationException(Resources.EventAggregatorNotConstructedOnUIThread);
                subscription = TaskEventSubscription<TPayload>.CreateForSynchronizationContext(actionReference, filterReference, SynchronizationContext);
                break;
            default:
                subscription = new AsyncEventSubscription<TPayload>(actionReference, filterReference);
                break;
        }

        return InternalSubscribe(subscription);
    }

    /// <summary>
    /// Begins publishing a payload to the current subscribers without returning a completion handle.
    /// </summary>
    /// <param name="payload">The payload delivered to subscribers whose filters accept it.</param>
    /// <remarks>
    /// Use <see cref="Send(TPayload)"/> to observe completion. Because this method is
    /// <see langword="async"/> <see langword="void"/>, exceptions raised after it returns cannot be
    /// observed through a task.
    /// </remarks>
    public async virtual void Publish(TPayload payload)
    {
        await PublishImplementation(payload);
    }

    /// <summary>
    /// Publishes a payload and returns a handle for the asynchronous publication operation.
    /// </summary>
    /// <param name="payload">The payload delivered to subscribers whose filters accept it.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes after publisher-thread callbacks have completed and
    /// scheduler-backed callbacks have been invoked by their scheduler.
    /// </returns>
    /// <remarks>
    /// Use this method instead of <see cref="Publish(TPayload)"/> when the publisher needs to observe
    /// completion or exceptions. Scheduler-backed subscriptions currently do not await the
    /// <see cref="ValueTask"/> returned by their callbacks.
    /// </remarks>
    public virtual ValueTask Send(TPayload payload)
    {
        return PublishImplementation(payload);
    }

    /// <summary>
    /// Extracts the payload from the object-array compatibility path and begins asynchronous publication.
    /// </summary>
    /// <param name="arguments">
    /// An object array whose first element is used as the payload; the default value of
    /// <typeparamref name="TPayload"/> is used when no value is supplied.
    /// </param>
    protected override async void InternalPublish(params object[] arguments)
    {
        TPayload argument = default;
        
        if (arguments != null && arguments.Length > 0 && arguments[0] != null)
        {
            argument = (TPayload)arguments[0];
        }

        await PublishImplementation(argument);
    }


    /// <summary>
    /// Publishes the payload sequentially to a snapshot of the active subscriptions.
    /// </summary>
    /// <param name="payload">The payload supplied by the publisher.</param>
    /// <returns>A <see cref="ValueTask"/> representing the publication loop.</returns>
    private async ValueTask PublishImplementation(TPayload payload)
    {
        var activeSubscribers = LightweightPubSubEvent.PruneSubscribers<AsyncEventSubscription<TPayload>>((List<IEventSubscription>)Subscriptions);

        try
        {
            var subscribtions = activeSubscribers.Subscribtions;

            for (int i = 0; i < activeSubscribers.Count; i++)
            {
                AsyncEventSubscription<TPayload> subscriber = subscribtions[i] as AsyncEventSubscription<TPayload>;

                if (subscriber != null)
                {
                    await subscriber.InvokeAction(payload);
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
    /// <param name="subscriber">The <see cref="Func{TPayload, ValueTask}"/> used when subscribing to the event.</param>
    public virtual void Unsubscribe(Func<TPayload, ValueTask> subscriber)
    {
        lock (Subscriptions)
        {
            IEventSubscription eventSubscription = Subscriptions.Cast<AsyncEventSubscription<TPayload>>().FirstOrDefault(evt => evt.Action == subscriber);
            if (eventSubscription != null)
            {
                Subscriptions.Remove(eventSubscription);
            }
        }
    }

    /// <summary>
    /// Determines whether the event contains a callback matching <paramref name="subscriber"/>.
    /// </summary>
    /// <param name="subscriber">The <see cref="Func{TPayload, ValueTask}"/> used when subscribing to the event.</param>
    /// <returns><see langword="true"/> if there is a <see cref="Func{TPayload, ValueTask}"/> that matches; otherwise <see langword="false"/>.</returns>
    public virtual bool Contains(Func<TPayload, ValueTask> subscriber)
    {
        IEventSubscription eventSubscription;
        lock (Subscriptions)
        {
            eventSubscription = Subscriptions.Cast<AsyncEventSubscription<TPayload>>().FirstOrDefault(evt => evt.Action == subscriber);
        }
        return eventSubscription != null;
    }
}
