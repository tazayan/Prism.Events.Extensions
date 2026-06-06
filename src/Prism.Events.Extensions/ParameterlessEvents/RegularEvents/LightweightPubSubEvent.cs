using System.Runtime.InteropServices;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Defines a class that manages publication and subscription to events.
/// </summary>
public class LightweightPubSubEvent : EventBase
{
    private volatile uint activePublishers;

    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// <see cref="LightweightPubSubEvent"/> will maintain a <see cref="WeakReference"/> to the target of the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// The PubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// PubSubEvent will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is raised.</param>
    /// <param name="threadOption">Specifies on which thread to receive the delegate callback.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// The PubSubEvent collection is thread-safe.
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
    /// The PubSubEvent collection is thread-safe.
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
    /// <param name="keepSubscriberReferenceAlive">When <see langword="true"/>, the <see cref="PubSubEvent"/> keeps a reference to the subscriber so it does not get garbage collected.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// If <paramref name="keepSubscriberReferenceAlive"/> is set to <see langword="false" />, <see cref="PubSubEvent"/> will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// If not using a WeakReference (<paramref name="keepSubscriberReferenceAlive"/> is <see langword="true" />), the user must explicitly call Unsubscribe for the event when disposing the subscriber in order to avoid memory leaks or unexpected behavior.
    /// <para/>
    /// The PubSubEvent collection is thread-safe.
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
        Interlocked.Increment(ref activePublishers);

        try
        {
            List<IEventSubscription> subscriptions = (List<IEventSubscription>)Subscriptions;

            //The collection of subscriptions may be modified by the subscribe operation which is performing only add operation
            //and it is okay to not observe that new element as prism events don't have contarctual agreemnt to observe it.
            //And in a worst case scenario when List is resized, the snapshot will still be valid and prevet old array from being GC'd.
            var snapshot = CollectionsMarshal.AsSpan(subscriptions);

            for (int i = 0; snapshot.Length > i; i++)
            {
                if (snapshot[i] is EventSubscription actionSubscription)
                {
                    actionSubscription.InvokeAction();
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref activePublishers);
        }

        if (activePublishers == 0)
            PruneSubscribers();
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

    private void PruneSubscribers()
    {
        if (Subscriptions.Count > 0)
        {
            lock (Subscriptions)
            {
                List<IEventSubscription> subscriptions = (List<IEventSubscription>)Subscriptions;

                for (var i = subscriptions.Count - 1; i >= 0; i--)
                {
                    var listItem = ((EventSubscription)subscriptions[i]).Action;

                    if (listItem == null)
                    {
                        // Prune from main list. Log?
                        subscriptions.RemoveAt(i);
                    }
                }
            }
        }
    }
}
