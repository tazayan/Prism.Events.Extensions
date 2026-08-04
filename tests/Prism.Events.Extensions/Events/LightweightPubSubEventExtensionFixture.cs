using System.Buffers;
using Xunit;

namespace Prism.Events.Extensions.Tests;

public class LightweightPubSubEventExtensionFixture
{
    [Fact]
    public void SinglePublishInvokesEachLightweightDelegateExactlyOnce()
    {
        var parameterlessActionCount = 0;
        var payloadFilterCount = 0;
        var payloadActionCount = 0;
        var parameterlessEvent = new LightweightPubSubEvent();
        var payloadEvent = new LightweightPubSubEvent<int>();
        parameterlessEvent.Subscribe(() => parameterlessActionCount++, true);
        payloadEvent.Subscribe(
            _ => payloadActionCount++,
            ThreadOption.PublisherThread,
            true,
            _ =>
            {
                payloadFilterCount++;
                return true;
            });

        parameterlessEvent.Publish();
        payloadEvent.Publish(7);

        Assert.Equal(1, parameterlessActionCount);
        Assert.Equal(1, payloadFilterCount);
        Assert.Equal(1, payloadActionCount);
    }

    [Fact]
    public void ParameterlessCompatibilityStrategyInvokesLiveActionAndReturnsNullForDeadAction()
    {
        var invocationCount = 0;
        var actionReference = new MockDelegateReference((Action)(() => invocationCount++));
        var subscription = new EventSubscription(actionReference);
        var strategy = ((IEventSubscription)subscription).GetExecutionStrategy();

        Assert.NotNull(strategy);
        strategy(Array.Empty<object>());
        Assert.Equal(1, invocationCount);

        actionReference.Target = null;

        Assert.Null(((IEventSubscription)subscription).GetExecutionStrategy());
        Assert.False(subscription.IsSubscriptionAlive());
    }

    [Fact]
    public void PayloadCompatibilityStrategyHandlesPayloadAndDefaultArgumentShapes()
    {
        var payloads = new List<int>();
        var actionReference = new MockDelegateReference((Action<int>)(payload => payloads.Add(payload)));
        var filterReference = new MockDelegateReference((Predicate<int>)(_ => true));
        var subscription = new EventSubscription<int>(actionReference, filterReference);
        var strategy = ((IEventSubscription)subscription).GetExecutionStrategy();

        Assert.NotNull(strategy);
        strategy(new object[] { 42 });
        strategy(null);
        strategy(Array.Empty<object>());
        strategy(new object[] { null });

        Assert.Equal(new[] { 42, 0, 0, 0 }, payloads);
    }

    [Fact]
    public void CompatibilityPublishHandlesPayloadAndDefaultArguments()
    {
        var pubSubEvent = new ExposedLightweightPubSubEvent<int>();
        var payloads = new List<int>();
        pubSubEvent.Subscribe(payloads.Add, true);

        pubSubEvent.PublishCompatibility(17);
        pubSubEvent.PublishCompatibility();
        pubSubEvent.PublishCompatibility((object)null);

        Assert.Equal(new[] { 17, 0, 0 }, payloads);
    }

    [Fact]
    public void UiSubscriptionsRequireAContextAndUnknownThreadOptionUsesPublisherSubscription()
    {
        var parameterlessEvent = new ExposedLightweightPubSubEvent();
        var payloadEvent = new ExposedLightweightPubSubEvent<int>();
        Action parameterlessAction = () => { };
        Action<int> payloadAction = _ => { };

        Assert.Throws<InvalidOperationException>(() => parameterlessEvent.Subscribe(parameterlessAction, ThreadOption.UIThread));
        Assert.Throws<InvalidOperationException>(() => payloadEvent.Subscribe(payloadAction, ThreadOption.UIThread));

        parameterlessEvent.Subscribe(parameterlessAction, (ThreadOption)(-1), true);
        payloadEvent.Subscribe(payloadAction, (ThreadOption)(-1), true);

        Assert.IsType<EventSubscription>(parameterlessEvent.SingleSubscription);
        Assert.IsType<EventSubscription<int>>(payloadEvent.SingleSubscription);
    }

    [Fact]
    public void UiPayloadSubscriptionExecutesFilterAndActionThroughContext()
    {
        var context = new ExecutingSynchronizationContext();
        var pubSubEvent = new LightweightPubSubEvent<int>
        {
            SynchronizationContext = context
        };
        var receivedPayload = 0;
        pubSubEvent.Subscribe(
            payload => receivedPayload = payload,
            ThreadOption.UIThread,
            true,
            payload => payload > 0);

        pubSubEvent.Publish(31);

        Assert.Equal(31, receivedPayload);
        Assert.Equal(1, context.PostCount);
    }

    [Fact]
    public void EmptyPublishReturnsAnEmptySnapshot()
    {
        var subscriptions = new List<IEventSubscription>();

        var snapshot = LightweightPubSubEvent.PruneSubscribers<EventSubscription>(subscriptions);

        Assert.Equal(0, snapshot.Count);
        Assert.Empty(snapshot.Subscribtions);
    }

    [Fact]
    public void GenericAndNonGenericEventsUseSeparateSubscriptionImplementations()
    {
        var parameterlessEvent = new ExposedLightweightPubSubEvent();
        var payloadEvent = new ExposedLightweightPubSubEvent<int>();
        parameterlessEvent.Subscribe(() => { }, true);
        payloadEvent.Subscribe(_ => { }, true);

        Assert.IsType<EventSubscription>(parameterlessEvent.SingleSubscription);
        Assert.IsType<EventSubscription<int>>(payloadEvent.SingleSubscription);
        Assert.NotEqual(parameterlessEvent.SingleSubscription.GetType(), payloadEvent.SingleSubscription.GetType());
    }

    [Fact]
    public void InternalSubscribeRejectsNullForBothEventShapes()
    {
        var parameterlessEvent = new ExposedLightweightPubSubEvent();
        var payloadEvent = new ExposedLightweightPubSubEvent<int>();

        Assert.Throws<ArgumentNullException>(() => parameterlessEvent.AddSubscription(null));
        Assert.Throws<ArgumentNullException>(() => payloadEvent.AddSubscription(null));
    }

    [Fact]
    public void SingleParameterlessPublishDoesNotAllocateCollectionSnapshot()
    {
        var pubSubEvent = new LightweightPubSubEvent();
        pubSubEvent.Subscribe(static () => { }, true);
        PrepareForAllocationMeasurement(pubSubEvent.Publish);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        pubSubEvent.Publish();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SingleValueTypePayloadPublishDoesNotBoxOrAllocateCollectionSnapshot()
    {
        var pubSubEvent = new LightweightPubSubEvent<int>();
        pubSubEvent.Subscribe(static _ => { }, true);
        PrepareForAllocationMeasurement(() => pubSubEvent.Publish(1));

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        pubSubEvent.Publish(1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(0, allocated);
    }

    private static void PrepareForAllocationMeasurement(Action publish)
    {
        for (var i = 0; i < 100; i++)
        {
            publish();
        }

        var snapshotBuffer = ArrayPool<IEventSubscription>.Shared.Rent(1);
        ArrayPool<IEventSubscription>.Shared.Return(snapshotBuffer, clearArray: true);
    }

    private sealed class ExposedLightweightPubSubEvent : LightweightPubSubEvent
    {
        public IEventSubscription SingleSubscription => Subscriptions.Single();

        public SubscriptionToken AddSubscription(IEventSubscription subscription)
        {
            return InternalSubscribe(subscription);
        }
    }

    private sealed class ExposedLightweightPubSubEvent<TPayload> : LightweightPubSubEvent<TPayload>
    {
        public IEventSubscription SingleSubscription => Subscriptions.Single();

        public SubscriptionToken AddSubscription(IEventSubscription subscription)
        {
            return InternalSubscribe(subscription);
        }

        public void PublishCompatibility(params object[] arguments)
        {
            InternalPublish(arguments);
        }
    }

    private sealed class ExecutingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback callback, object state)
        {
            PostCount++;
            callback(state);
        }
    }
}
