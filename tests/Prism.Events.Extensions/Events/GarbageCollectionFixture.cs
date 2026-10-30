using Xunit;

namespace Prism.Events.Extensions.Tests;

/// <summary>
/// Verifies how subscriptions interact with the garbage collector: weakly referenced subscribers do
/// not stay alive because of an event, strongly referenced subscribers do, and neither the event nor
/// its pooled publication snapshots keep a subscriber alive after it unsubscribes.
/// </summary>
public class GarbageCollectionFixture
{
    public static TheoryData<ThreadOption> ThreadOptions => new()
    {
        ThreadOption.PublisherThread,
        ThreadOption.BackgroundThread,
        ThreadOption.UIThread,
    };

    [Theory]
    [MemberData(nameof(ThreadOptions))]
    public void LightweightEventDoesNotKeepWeakSubscriberAliveAndPrunesIt(ThreadOption threadOption)
    {
        var pubSubEvent = new InspectableLightweightPubSubEvent { SynchronizationContext = new RecordingSynchronizationContext() };
        var subscriber = GarbageCollection.CreateSubscriber(s => pubSubEvent.Subscribe(s.Handle, threadOption));
        Assert.Equal(1, pubSubEvent.SubscriptionCount);

        GarbageCollection.Collect();
        Assert.False(subscriber.IsAlive);

        pubSubEvent.Publish();
        Assert.Equal(0, pubSubEvent.SubscriptionCount);
        Assert.Equal(0, ((RecordingSynchronizationContext)pubSubEvent.SynchronizationContext).PostedCount);
    }

    [Theory]
    [MemberData(nameof(ThreadOptions))]
    public void LightweightPayloadEventDoesNotKeepWeakSubscriberAliveAndPrunesIt(ThreadOption threadOption)
    {
        var pubSubEvent = new InspectableLightweightPubSubEvent<int> { SynchronizationContext = new RecordingSynchronizationContext() };
        var subscriber = GarbageCollection.CreateSubscriber(s => pubSubEvent.Subscribe(s.Handle, threadOption));

        GarbageCollection.Collect();
        Assert.False(subscriber.IsAlive);

        pubSubEvent.Publish(1);
        Assert.Equal(0, pubSubEvent.SubscriptionCount);
        Assert.Equal(0, ((RecordingSynchronizationContext)pubSubEvent.SynchronizationContext).PostedCount);
    }

    [Theory]
    [MemberData(nameof(ThreadOptions))]
    public async Task AsyncEventDoesNotKeepWeakSubscriberAliveAndPrunesIt(ThreadOption threadOption)
    {
        var pubSubEvent = new InspectableAsyncPubSubEvent { SynchronizationContext = new RecordingSynchronizationContext() };
        var subscriber = GarbageCollection.CreateSubscriber(s => pubSubEvent.Subscribe(s.HandleAsync, threadOption));

        GarbageCollection.Collect();
        Assert.False(subscriber.IsAlive);

        await pubSubEvent.Send();
        Assert.Equal(0, pubSubEvent.SubscriptionCount);
        Assert.Equal(0, ((RecordingSynchronizationContext)pubSubEvent.SynchronizationContext).PostedCount);
    }

    [Theory]
    [MemberData(nameof(ThreadOptions))]
    public async Task AsyncPayloadEventDoesNotKeepWeakSubscriberAliveAndPrunesIt(ThreadOption threadOption)
    {
        var pubSubEvent = new InspectableAsyncPubSubEvent<int> { SynchronizationContext = new RecordingSynchronizationContext() };
        var subscriber = GarbageCollection.CreateSubscriber(s => pubSubEvent.Subscribe(s.HandleAsync, threadOption));

        GarbageCollection.Collect();
        Assert.False(subscriber.IsAlive);

        await pubSubEvent.Send(1);
        Assert.Equal(0, pubSubEvent.SubscriptionCount);
        Assert.Equal(0, ((RecordingSynchronizationContext)pubSubEvent.SynchronizationContext).PostedCount);
    }

    [Fact]
    public async Task CollectedWeakFilterPrunesPayloadSubscriptionsEvenWhenTheCallbackIsAlive()
    {
        var callbackOwner = new Subscriber();
        var lightweightEvent = new InspectableLightweightPubSubEvent<int>();
        var asyncEvent = new InspectableAsyncPubSubEvent<int>();
        var filterOwner = GarbageCollection.CreateSubscriber(s =>
        {
            lightweightEvent.Subscribe(callbackOwner.Handle, ThreadOption.PublisherThread, false, s.Accept);
            asyncEvent.Subscribe(callbackOwner.HandleAsync, ThreadOption.PublisherThread, false, s.Accept);
        });

        GarbageCollection.Collect();
        Assert.False(filterOwner.IsAlive);

        lightweightEvent.Publish(1);
        await asyncEvent.Send(1);

        Assert.Equal(0, lightweightEvent.SubscriptionCount);
        Assert.Equal(0, asyncEvent.SubscriptionCount);
        Assert.Equal(0, callbackOwner.InvocationCount);
    }

    [Fact]
    public async Task StronglyReferencedSubscribersSurviveCollectionAndKeepReceivingEvents()
    {
        var lightweightEvent = new LightweightPubSubEvent();
        var lightweightPayloadEvent = new LightweightPubSubEvent<int>();
        var asyncEvent = new AsyncPubSubEvent();
        var asyncPayloadEvent = new AsyncPubSubEvent<int>();
        var subscriber = GarbageCollection.CreateSubscriber(s =>
        {
            lightweightEvent.Subscribe(s.Handle, keepSubscriberReferenceAlive: true);
            lightweightPayloadEvent.Subscribe(s.Handle, keepSubscriberReferenceAlive: true);
            asyncEvent.Subscribe(s.HandleAsync, keepSubscriberReferenceAlive: true);
            asyncPayloadEvent.Subscribe(s.HandleAsync, keepSubscriberReferenceAlive: true);
        });

        GarbageCollection.Collect();
        Assert.True(subscriber.IsAlive);

        lightweightEvent.Publish();
        lightweightPayloadEvent.Publish(1);
        await asyncEvent.Send();
        await asyncPayloadEvent.Send(1);

        Assert.Equal(4, ((Subscriber)subscriber.Target).InvocationCount);
    }

    [Fact]
    public async Task UnsubscribedStrongSubscriberIsNotRetainedByTheEventOrThePooledSnapshot()
    {
        var lightweightEvent = new LightweightPubSubEvent();
        var lightweightPayloadEvent = new LightweightPubSubEvent<int>();
        var asyncEvent = new AsyncPubSubEvent();
        var asyncPayloadEvent = new AsyncPubSubEvent<int>();
        var tokens = new List<SubscriptionToken>();
        var subscriber = GarbageCollection.CreateSubscriber(s =>
        {
            tokens.Add(lightweightEvent.Subscribe(s.Handle, keepSubscriberReferenceAlive: true));
            tokens.Add(lightweightPayloadEvent.Subscribe(s.Handle, keepSubscriberReferenceAlive: true));
            tokens.Add(asyncEvent.Subscribe(s.HandleAsync, keepSubscriberReferenceAlive: true));
            tokens.Add(asyncPayloadEvent.Subscribe(s.HandleAsync, keepSubscriberReferenceAlive: true));
        });

        // Publishing copies the subscriptions into arrays rented from ArrayPool<T>.Shared. Those
        // arrays must be cleared when they are returned, or the pool would keep the subscriber alive.
        lightweightEvent.Publish();
        lightweightPayloadEvent.Publish(1);
        await asyncEvent.Send();
        await asyncPayloadEvent.Send(1);

        foreach (var token in tokens)
        {
            token.Dispose();
        }

        GarbageCollection.Collect();
        Assert.False(subscriber.IsAlive);
    }

    [Fact]
    public void EventBasePruneRemovesCollectedSubscriptionsFromEveryEventType()
    {
        var lightweightEvent = new InspectableLightweightPubSubEvent();
        var lightweightPayloadEvent = new InspectableLightweightPubSubEvent<int>();
        var asyncEvent = new InspectableAsyncPubSubEvent();
        var asyncPayloadEvent = new InspectableAsyncPubSubEvent<int>();
        var keptAlive = new Subscriber();
        lightweightEvent.Subscribe(keptAlive.Handle);
        lightweightPayloadEvent.Subscribe(keptAlive.Handle);
        asyncEvent.Subscribe(keptAlive.HandleAsync);
        asyncPayloadEvent.Subscribe(keptAlive.HandleAsync);
        var collected = GarbageCollection.CreateSubscriber(s =>
        {
            lightweightEvent.Subscribe(s.Handle);
            lightweightPayloadEvent.Subscribe(s.Handle);
            asyncEvent.Subscribe(s.HandleAsync);
            asyncPayloadEvent.Subscribe(s.HandleAsync);
        });

        GarbageCollection.Collect();
        Assert.False(collected.IsAlive);

        lightweightEvent.Prune();
        lightweightPayloadEvent.Prune();
        asyncEvent.Prune();
        asyncPayloadEvent.Prune();

        Assert.Equal(1, lightweightEvent.SubscriptionCount);
        Assert.Equal(1, lightweightPayloadEvent.SubscriptionCount);
        Assert.Equal(1, asyncEvent.SubscriptionCount);
        Assert.Equal(1, asyncPayloadEvent.SubscriptionCount);
        GC.KeepAlive(keptAlive);
    }

    [Fact]
    public void EventAndStronglyReferencedSubscriberAreCollectedTogether()
    {
        var references = CreateEventWithStrongSubscriber();

        GarbageCollection.Collect();

        Assert.False(references.Event.IsAlive);
        Assert.False(references.Subscriber.IsAlive);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WeakReference Event, WeakReference Subscriber) CreateEventWithStrongSubscriber()
    {
        var pubSubEvent = new LightweightPubSubEvent<int>();
        var subscriber = new Subscriber();
        pubSubEvent.Subscribe(subscriber.Handle, keepSubscriberReferenceAlive: true);
        pubSubEvent.Publish(1);
        return (new WeakReference(pubSubEvent), new WeakReference(subscriber));
    }
}
