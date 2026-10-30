using System.Buffers;
using Xunit;

namespace Prism.Events.Extensions.Tests;

/// <summary>
/// Covers edge cases that the scenario-based fixtures do not reach: dead delegates at invocation time,
/// the <see cref="EventBase"/> compatibility paths, exception routing of <see langword="async"/>
/// <see langword="void"/> publication, and <see cref="TaskSchedulerHelper"/>.
/// </summary>
public class EdgeCaseFixture
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void ParameterlessSubscriptionsDoNothingWhenTheCallbackIsNoLongerAlive()
    {
        var invocationCount = 0;
        var context = new RecordingSynchronizationContext();
        var publisherReference = new MockDelegateReference((Action)(() => invocationCount++), preventTargetSet: false);
        var backgroundReference = new MockDelegateReference((Action)(() => invocationCount++), preventTargetSet: false);
        var dispatcherReference = new MockDelegateReference((Action)(() => invocationCount++), preventTargetSet: false);
        var publisher = new EventSubscription(publisherReference);
        var background = new BackgroundEventSubscription(backgroundReference);
        var dispatcher = new DispatcherEventSubscription(dispatcherReference, context);
        publisherReference.Target = null;
        backgroundReference.Target = null;
        dispatcherReference.Target = null;

        publisher.InvokeAction();
        background.InvokeAction();
        dispatcher.InvokeAction();

        Assert.Equal(0, invocationCount);
        Assert.Equal(0, context.PostedCount);
        Assert.False(publisher.IsSubscriptionAlive());
    }

    [Fact]
    public void PayloadSubscriptionsDoNothingWhenOnlyTheFilterIsNoLongerAlive()
    {
        var invocationCount = 0;
        var context = new RecordingSynchronizationContext();
        Action<int> action = _ => invocationCount++;
        var backgroundFilter = new MockDelegateReference((Predicate<int>)(_ => true), preventTargetSet: false);
        var dispatcherFilter = new MockDelegateReference((Predicate<int>)(_ => true), preventTargetSet: false);
        var background = new BackgroundEventSubscription<int>(new MockDelegateReference(action), backgroundFilter);
        var dispatcher = new DispatcherEventSubscription<int>(new MockDelegateReference(action), dispatcherFilter, context);
        backgroundFilter.Target = null;
        dispatcherFilter.Target = null;

        background.InvokeAction(1);
        dispatcher.InvokeAction(1);

        Assert.Equal(0, invocationCount);
        Assert.Equal(0, context.PostedCount);
    }

    [Fact]
    public void PayloadSubscriptionsDoNothingWhenTheCallbackIsNoLongerAlive()
    {
        var invocationCount = 0;
        var context = new RecordingSynchronizationContext();
        var publisherAction = new MockDelegateReference((Action<int>)(_ => invocationCount++), preventTargetSet: false);
        var backgroundAction = new MockDelegateReference((Action<int>)(_ => invocationCount++), preventTargetSet: false);
        var dispatcherAction = new MockDelegateReference((Action<int>)(_ => invocationCount++), preventTargetSet: false);
        var filter = new MockDelegateReference((Predicate<int>)(_ => true));
        var publisher = new EventSubscription<int>(publisherAction, filter);
        var background = new BackgroundEventSubscription<int>(backgroundAction, filter);
        var dispatcher = new DispatcherEventSubscription<int>(dispatcherAction, filter, context);
        publisherAction.Target = null;
        backgroundAction.Target = null;
        dispatcherAction.Target = null;

        publisher.InvokeAction(1);
        background.InvokeAction(1);
        dispatcher.InvokeAction(1);

        Assert.Equal(0, invocationCount);
        Assert.Equal(0, context.PostedCount);
        Assert.False(publisher.IsSubscriptionAlive());
    }

    [Fact]
    public void PayloadDispatcherSubscriptionPostsButSkipsTheCallbackWhenTheFilterRejectsThePayload()
    {
        var payloads = new List<int>();
        var context = new RecordingSynchronizationContext();
        var dispatcher = new DispatcherEventSubscription<int>(
            new MockDelegateReference((Action<int>)payloads.Add),
            new MockDelegateReference((Predicate<int>)(payload => payload > 0)),
            context);

        dispatcher.InvokeAction(-1);
        dispatcher.InvokeAction(1);

        Assert.Equal(2, context.PostedCount);
        Assert.Empty(context.RunAll());
        Assert.Equal(new[] { 1 }, payloads);
    }

    [Fact]
    public async Task PayloadBackgroundSubscriptionSkipsTheCallbackWhenTheFilterRejectsThePayload()
    {
        var filterCalls = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        var background = new BackgroundEventSubscription<int>(
            new MockDelegateReference((Action<int>)(_ => Interlocked.Increment(ref invocationCount))),
            new MockDelegateReference((Predicate<int>)(payload =>
            {
                filterCalls.TrySetResult(payload);
                return false;
            })));

        background.InvokeAction(5);

        Assert.Equal(5, await filterCalls.Task.WaitAsync(TestTimeout));
        await Task.Delay(50);
        Assert.Equal(0, invocationCount);
    }

    [Fact]
    public void ParameterlessLightweightEventPublishesThroughTheEventBaseCompatibilityPath()
    {
        var pubSubEvent = new InspectableLightweightPubSubEvent();
        var invocationCount = 0;
        pubSubEvent.Subscribe(() => invocationCount++, keepSubscriberReferenceAlive: true);

        pubSubEvent.PublishThroughEventBase();
        pubSubEvent.PublishThroughEventBase("ignored", 42);

        Assert.Equal(2, invocationCount);
    }

    [Fact]
    public void LightweightPublishPropagatesTheFirstSubscriberExceptionAndSkipsTheRest()
    {
        var parameterlessEvent = new LightweightPubSubEvent();
        var payloadEvent = new LightweightPubSubEvent<int>();
        var laterCalls = 0;
        var parameterlessToken = parameterlessEvent.Subscribe(() => throw new InvalidOperationException("parameterless"), true);
        parameterlessEvent.Subscribe(() => laterCalls++, true);
        var payloadToken = payloadEvent.Subscribe(_ => throw new InvalidOperationException("payload"), true);
        payloadEvent.Subscribe(_ => laterCalls++, true);

        Assert.Equal("parameterless", Assert.Throws<InvalidOperationException>(() => parameterlessEvent.Publish()).Message);
        Assert.Equal("payload", Assert.Throws<InvalidOperationException>(() => payloadEvent.Publish(1)).Message);
        Assert.Equal(0, laterCalls);

        // A failed publication still releases its snapshot, so later publications work normally.
        parameterlessToken.Dispose();
        payloadToken.Dispose();
        parameterlessEvent.Publish();
        payloadEvent.Publish(1);
        Assert.Equal(2, laterCalls);
    }

    [Fact]
    public async Task SendSkipsTheRemainingSubscribersAfterAFailure()
    {
        var pubSubEvent = new AsyncPubSubEvent<int>();
        var laterCalls = 0;
        pubSubEvent.Subscribe(_ => throw new InvalidOperationException("synchronous throw"), true);
        pubSubEvent.Subscribe(_ =>
        {
            laterCalls++;
            return ValueTask.CompletedTask;
        }, true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => pubSubEvent.Send(1).AsTask());

        Assert.Equal("synchronous throw", exception.Message);
        Assert.Equal(0, laterCalls);
    }

    [Fact]
    public void AsyncPublishRaisesSubscriberExceptionsOnTheCallersSynchronizationContext()
    {
        var parameterlessEvent = new AsyncPubSubEvent();
        var payloadEvent = new AsyncPubSubEvent<int>();
        parameterlessEvent.Subscribe(() => ValueTask.FromException(new InvalidOperationException("parameterless")), true);
        payloadEvent.Subscribe(_ => ValueTask.FromException(new InvalidOperationException("payload")), true);
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;

        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            parameterlessEvent.Publish();
            payloadEvent.Publish(1);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var exceptions = context.RunAll();
        Assert.Equal(new[] { "parameterless", "payload" }, exceptions.Select(exception => exception.Message));
    }

    [Fact]
    public void TaskSchedulerHelperRunsTasksThroughTheContextAndRestoresThePreviousContext()
    {
        var target = new RecordingSynchronizationContext();
        var previous = new RecordingSynchronizationContext();
        var original = SynchronizationContext.Current;
        TaskScheduler scheduler;

        try
        {
            SynchronizationContext.SetSynchronizationContext(previous);
            scheduler = TaskSchedulerHelper.FromSynchronizationContext(target);
            Assert.Same(previous, SynchronizationContext.Current);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }

        var ran = false;
        var task = Task.Factory.StartNew(() => ran = true, CancellationToken.None, TaskCreationOptions.None, scheduler);

        Assert.Equal(1, target.PostedCount);
        Assert.Equal(0, previous.PostedCount);
        Assert.Empty(target.RunAll());
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(ran);
    }

    [Fact]
    public void TaskSchedulerHelperRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => TaskSchedulerHelper.FromSynchronizationContext(null));
    }

    [Fact]
    public void InvalidDelegateReferenceMessagesNameTheExpectedDelegateType()
    {
        var wrong = new MockDelegateReference((Action<string>)(_ => { }));
        var filter = new MockDelegateReference((Predicate<int>)(_ => true));

        Assert.Contains(typeof(Action).FullName, Assert.Throws<ArgumentException>(() => new EventSubscription(wrong)).Message);
        Assert.Contains(typeof(Action<int>).FullName, Assert.Throws<ArgumentException>(() => new EventSubscription<int>(wrong, filter)).Message);
        Assert.Contains(typeof(Predicate<int>).FullName, Assert.Throws<ArgumentException>(() => new EventSubscription<int>(new MockDelegateReference((Action<int>)(_ => { })), wrong)).Message);
        Assert.Contains(typeof(Func<ValueTask>).FullName, Assert.Throws<ArgumentException>(() => new AsyncEventSubscription(wrong)).Message);
        Assert.Contains(typeof(Func<int, ValueTask>).FullName, Assert.Throws<ArgumentException>(() => new AsyncEventSubscription<int>(wrong, filter)).Message);
    }

    [Fact]
    public void SnapshotCopiesEverySubscriptionInOrderWithoutCheckingLiveness()
    {
        var first = new EventSubscription(new MockDelegateReference((Action)(() => { })));
        var dead = new MockDelegateReference((Action)(() => { }), preventTargetSet: false);
        var deadSubscription = new EventSubscription(dead);
        var second = new EventSubscription(new MockDelegateReference((Action)(() => { })));
        var subscriptions = new List<IEventSubscription> { first, deadSubscription, second };
        dead.Target = null;

        var snapshot = LightweightPubSubEvent.PruneSubscribers<EventSubscription>(subscriptions);

        Assert.Equal(2, snapshot.Count);
        Assert.Same(first, (EventSubscription)snapshot.Subscriptions[0]);
        Assert.Same(second, (EventSubscription)snapshot.Subscriptions[1]);


        if (snapshot.Count > 0)
        {
            ArrayPool<IEventSubscription>.Shared.Return(snapshot.Subscriptions, clearArray: true);
        }
        Assert.Equal(new IEventSubscription[] { first, second }, subscriptions);
    }

    [Fact]
    public async Task DeadSubscriptionsAreRemovedAfterThePublicationThatFindsThem()
    {
        var lightweightEvent = new InspectableLightweightPubSubEvent();
        var lightweightPayloadEvent = new InspectableLightweightPubSubEvent<int>();
        var asyncEvent = new InspectableAsyncPubSubEvent();
        var asyncPayloadEvent = new InspectableAsyncPubSubEvent<int>();
        var live = new Subscriber();
        lightweightEvent.Subscribe(live.Handle);
        lightweightPayloadEvent.Subscribe(live.Handle);
        asyncEvent.Subscribe(live.HandleAsync);
        asyncPayloadEvent.Subscribe(live.HandleAsync);
        var collected = GarbageCollection.CreateSubscriber(s =>
        {
            lightweightEvent.Subscribe(s.Handle);
            lightweightPayloadEvent.Subscribe(s.Handle);
            asyncEvent.Subscribe(s.HandleAsync);
            asyncPayloadEvent.Subscribe(s.HandleAsync);
        });
        GarbageCollection.Collect();
        Assert.False(collected.IsAlive);

        lightweightEvent.Publish();
        lightweightPayloadEvent.Publish(1);
        await asyncEvent.Send();
        await asyncPayloadEvent.Send(1);

        Assert.Equal(4, live.InvocationCount);
        Assert.Equal(1, lightweightEvent.SubscriptionCount);
        Assert.Equal(1, lightweightPayloadEvent.SubscriptionCount);
        Assert.Equal(1, asyncEvent.SubscriptionCount);
        Assert.Equal(1, asyncPayloadEvent.SubscriptionCount);
    }

    [Fact]
    public void CompatibilityPathRejectsPayloadsOfTheWrongType()
    {
        IEventSubscription subscription = new EventSubscription<int>(
            new MockDelegateReference((Action<int>)(_ => { })),
            new MockDelegateReference((Predicate<int>)(_ => true)));

        Assert.Throws<InvalidCastException>(() => subscription.GetExecutionStrategy()(new object[] { "not an int" }));
    }
}
