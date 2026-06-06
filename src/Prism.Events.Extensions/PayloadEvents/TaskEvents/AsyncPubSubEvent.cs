using System.Buffers;
using System.Runtime.InteropServices;
using Prism.Events.Extensions.Properties;

namespace Prism.Events.Extensions;

/// <summary>
/// Defines a class that manages publication and subscription to events.
/// </summary>

public class AsyncPubSubEvent<TPayload> : EventBase
{
    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// <see cref="AsyncPubSubEvent{TPayload}"/> will maintain a <see cref="WeakReference"/> to the target of the supplied <paramref name="action"/> delegate.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// The PubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Func<TPayload, ValueTask> action)
    {
        return Subscribe(action, ThreadOption.PublisherThread);
    }

    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is raised.</param>
    /// <param name="filter">Filter to evaluate if the subscriber should receive the event.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    public virtual SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, Predicate<TPayload> filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
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
    public SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false);
    }

    /// <summary>
    /// Subscribes a delegate to an event that will be published on the <see cref="ThreadOption.PublisherThread"/>.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <param name="keepSubscriberReferenceAlive">When <see langword="true"/>, the <see cref="LightweightPubSubEvent{TPayload}"/> keeps a reference to the subscriber so it does not get garbage collected.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// If <paramref name="keepSubscriberReferenceAlive"/> is set to <see langword="false" />, <see cref="LightweightPubSubEvent{TPayload}"/> will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// If not using a WeakReference (<paramref name="keepSubscriberReferenceAlive"/> is <see langword="true" />), the user must explicitly call Unsubscribe for the event when disposing the subscriber in order to avoid memory leaks or unexpected behavior.
    /// <para/>
    /// The PubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <param name="threadOption">Specifies on which thread to receive the delegate callback.</param>
    /// <param name="keepSubscriberReferenceAlive">When <see langword="true"/>, the <see cref="LightweightPubSubEvent{TPayload}"/> keeps a reference to the subscriber so it does not get garbage collected.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// If <paramref name="keepSubscriberReferenceAlive"/> is set to <see langword="false" />, <see cref="LightweightPubSubEvent{TPayload}"/> will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// If not using a WeakReference (<paramref name="keepSubscriberReferenceAlive"/> is <see langword="true" />), the user must explicitly call Unsubscribe for the event when disposing the subscriber in order to avoid memory leaks or unexpected behavior.
    /// <para/>
    /// The PubSubEvent collection is thread-safe.
    /// </remarks>
    public SubscriptionToken Subscribe(Func<TPayload, ValueTask> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    /// <summary>
    /// Subscribes a delegate to an event.
    /// </summary>
    /// <param name="action">The delegate that gets executed when the event is published.</param>
    /// <param name="threadOption">Specifies on which thread to receive the delegate callback.</param>
    /// <param name="keepSubscriberReferenceAlive">When <see langword="true"/>, the <see cref="LightweightPubSubEvent{TPayload}"/> keeps a reference to the subscriber so it does not get garbage collected.</param>
    /// <param name="filter">Filter to evaluate if the subscriber should receive the event.</param>
    /// <returns>A <see cref="SubscriptionToken"/> that uniquely identifies the added subscription.</returns>
    /// <remarks>
    /// If <paramref name="keepSubscriberReferenceAlive"/> is set to <see langword="false" />, <see cref="LightweightPubSubEvent{TPayload}"/> will maintain a <see cref="WeakReference"/> to the Target of the supplied <paramref name="action"/> delegate.
    /// If not using a WeakReference (<paramref name="keepSubscriberReferenceAlive"/> is <see langword="true" />), the user must explicitly call Unsubscribe for the event when disposing the subscriber in order to avoid memory leaks or unexpected behavior.
    ///
    /// The PubSubEvent collection is thread-safe.
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
    /// Publishes the <see cref="LightweightPubSubEvent{TPayload}"/>.
    /// </summary>
    /// <param name="payload">Message to pass to the subscribers.</param>
    public virtual async ValueTask Publish(TPayload payload)
    {
        var activeSubscribers = AsyncPubSubEvent.PruneSubscribers((List<IEventSubscription>)Subscriptions);

        try
        {
            foreach (AsyncEventSubscription<TPayload> subscriber in activeSubscribers)
            {
                await subscriber.InvokeAction(payload);
            }
        }
        finally
        {
            ArrayPool<IEventSubscription>.Shared.Return(activeSubscribers);
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
    /// Removes the first subscriber matching <see cref="Action{TPayload}"/> from the subscribers' list.
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
    /// Returns <see langword="true"/> if there is a subscriber matching <see cref="Action{TPayload}"/>.
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
