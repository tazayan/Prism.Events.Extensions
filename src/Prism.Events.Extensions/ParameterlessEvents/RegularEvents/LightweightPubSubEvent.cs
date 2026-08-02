using System.Buffers;
using System.Runtime.InteropServices;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Defines a class that manages publication and subscription to events.
/// </summary>
public class LightweightPubSubEvent : EventBase
{
    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// <see cref="LightweightPubSubEvent"/> will maintain a <see cref="WeakReference"/> to the target of the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// The LightweightPubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// LightweightPubSubEvent will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is raised.</param>
    /// <param name="threadOption">Specifies on which thread to receive the delegate callback.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// The LightweightPubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false);
    }

    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <param name="keepSubscriberReferenceAlive">When <see langword="true"/>, the <see cref="LightweightPubSubEvent"/> keeps a reference to the subscriber so it does not get garbage collected.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// If <paramref name="keepSubscriberReferenceAlive"/> is set to <see langword="false" />, <see cref="LightweightPubSubEvent"/> will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// If not using a WeakReference (<paramref name="keepSubscriberReferenceAlive"/> is <see langword="true" />), the user must explicitly call Unsubscribe for the event when disposing the subscriber in order to avoid memory leaks or unexpected behavior.
    /// <para/>
    /// The LightweightPubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <param name="threadOption">Specifies on which thread to receive the delegate callback.</param>
    /// <param name="keepSubscriberReferenceAlive">When <see langword="true"/>, the <see cref="LightweightPubSubEvent"/> keeps a reference to the subscriber so it does not get garbage collected.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// If <paramref name="keepSubscriberReferenceAlive"/> is set to <see langword="false" />, <see cref="LightweightPubSubEvent"/> will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// If not using a WeakReference (<paramref name="keepSubscriberReferenceAlive"/> is <see langword="true" />), the user must explicitly call Unsubscribe for the event when disposing the subscriber in order to avoid memory leaks or unexpected behavior.
    /// <para/>
    /// The LightweightPubSubEvent collection is thread-safe.
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
    /// Publishes the <see cref="LightweightPubSubEvent"/>.
    /// </summary>
    public virtual void Publish()
    {
        InternalPublish();
    }

    /// <inheritdoc/>
    protected override void InternalPublish(params object[] arguments)
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
                        var isActionAlive = ((TSubscriptionType)subscriptions[i]).IsActionAlive();

                        if (!isActionAlive)
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
