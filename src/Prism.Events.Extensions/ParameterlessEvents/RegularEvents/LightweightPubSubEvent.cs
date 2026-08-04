using System.Buffers;
using System.Runtime.InteropServices;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Represents a parameterless publish/subscribe event with an allocation-optimized publication path.
/// </summary>
/// <remarks>
/// Publication invokes strongly typed subscription methods directly instead of creating
/// <see cref="IEventSubscription.GetExecutionStrategy"/> delegates for each subscriber.
/// Each publication operates on a stable snapshot of the subscribers observed at its start.
/// </remarks>
public class LightweightPubSubEvent : EventBase
{
    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// <see cref="LightweightPubSubEvent"/> will maintain a <see cref="WeakReference"/> to the target of the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The callback invoked when the event is published.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// <see cref="LightweightPubSubEvent"/> maintains a <see cref="WeakReference"/> to the target of
    /// the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The callback invoked when the event is published.</param>
    /// <param name="threadOption">The thread on which the callback is dispatched.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="threadOption"/> is <see cref="ThreadOption.UIThread"/> and no
    /// <see cref="EventBase.SynchronizationContext"/> is available.
    /// </exception>
    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false);
    }

    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The callback invoked when the event is published.</param>
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
    public SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// </summary>
    /// <param name="action">The callback invoked when the event is published.</param>
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
    public virtual SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        IDelegateReference actionReference = new DelegateReference(action, keepSubscriberReferenceAlive);

        EventSubscription subscription;
        switch (threadOption)
        {
            case ThreadOption.PublisherThread:
                subscription = new EventSubscription(actionReference);
                break;
            case ThreadOption.BackgroundThread:
                subscription = new BackgroundEventSubscription(actionReference);
                break;
            case ThreadOption.UIThread:
                if (SynchronizationContext == null)
                    throw new InvalidOperationException(Resources.EventAggregatorNotConstructedOnUIThread);
                subscription = new DispatcherEventSubscription(actionReference, SynchronizationContext);
                break;
            default:
                subscription = new EventSubscription(actionReference);
                break;
        }

        return InternalSubscribe(subscription);
    }

    /// <summary>
    /// Publishes the event to a snapshot of the current subscribers.
    /// </summary>
    public virtual void Publish()
    {
        InternalPublish();
    }

    /// <inheritdoc/>
    protected override void InternalPublish(params object[] arguments)
    {
        PublishImplementation();
    }

    /// <summary>
    /// Publishes the event through the allocation-optimized subscription path.
    /// </summary>
    protected void PublishImplementation()
    {
        var activeSubscribers = LightweightPubSubEvent.PruneSubscribers<EventSubscription>((List<IEventSubscription>)Subscriptions);

        try
        {
            var subscribtions = activeSubscribers.Subscribtions;

            for (int i = 0; i < activeSubscribers.Count; i++)
            {
                EventSubscription subscriber = subscribtions[i] as EventSubscription;

                if (subscriber != null)
                {
                    subscriber.InvokeAction();
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
    /// Removes the first subscriber matching <see cref="Action"/> from the subscribers' list.
    /// </summary>
    /// <param name="subscriber">The <see cref="Action"/> used when subscribing to the event.</param>
    public virtual void Unsubscribe(Action subscriber)
    {
        lock (Subscriptions)
        {
            IEventSubscription eventSubscription = Subscriptions.Cast<EventSubscription>().FirstOrDefault(evt => evt.Action == subscriber);
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
    public virtual bool Contains(Action subscriber)
    {
        IEventSubscription eventSubscription;
        lock (Subscriptions)
        {
            eventSubscription = Subscriptions.Cast<EventSubscription>().FirstOrDefault(evt => evt.Action == subscriber);
        }
        return eventSubscription != null;
    }

    /// <summary>
    /// Removes subscriptions whose required delegates are no longer alive and creates a stable
    /// snapshot of the remaining subscriptions.
    /// </summary>
    /// <typeparam name="TSubscriptionType">
    /// The concrete subscription type stored in <paramref name="subscriptions"/>.
    /// </typeparam>
    /// <param name="subscriptions">The synchronized subscription list to prune and copy.</param>
    /// <returns>
    /// A pooled array containing the subscription snapshot and the number of valid entries in that array.
    /// </returns>
    internal static (IEventSubscription[] Subscribtions, int Count) PruneSubscribers<TSubscriptionType>(List<IEventSubscription> subscriptions) where TSubscriptionType : IEventActionProvider
    {
        if (subscriptions.Count > 0)
        {
            lock (subscriptions)
            {
                if (subscriptions.Count > 0)
                {

                    for (var i = subscriptions.Count - 1; i >= 0; i--)
                    {
                        var isSubscriptionAlive = ((TSubscriptionType)subscriptions[i]).IsSubscriptionAlive();

                        if (!isSubscriptionAlive)
                        {
                            // Prune from main list
                            subscriptions.RemoveAt(i);
                        }
                    }

                    ArrayPool<IEventSubscription> pool = ArrayPool<IEventSubscription>.Shared;
                    var rentedArray = pool.Rent(subscriptions.Count);
                    subscriptions.CopyTo(rentedArray);
                    return (rentedArray, subscriptions.Count);
                }
                else
                {
                    return (Array.Empty<IEventSubscription>(), 0);
                }
            }
        }

        return (Array.Empty<IEventSubscription>(), 0);
    }
}
