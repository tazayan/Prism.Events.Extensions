using Xunit;

namespace Prism.Events.Extensions.Tests;

public class AsyncPubSubEventExtensionFixture
{
    [Fact]
    public async Task SinglePublishInvokesEachAsyncDelegateExactlyOnce()
    {
        var parameterlessActionCount = 0;
        var payloadFilterCount = 0;
        var payloadActionCount = 0;
        var parameterlessInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var payloadInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parameterlessEvent = new AsyncPubSubEvent();
        var payloadEvent = new AsyncPubSubEvent<int>();
        parameterlessEvent.Subscribe(() =>
        {
            parameterlessActionCount++;
            parameterlessInvoked.TrySetResult(true);
            return ValueTask.CompletedTask;
        }, true);
        payloadEvent.Subscribe(
            _ =>
            {
                payloadActionCount++;
                payloadInvoked.TrySetResult(true);
                return ValueTask.CompletedTask;
            },
            ThreadOption.PublisherThread,
            true,
            _ =>
            {
                payloadFilterCount++;
                return true;
            });

        parameterlessEvent.Publish();
        payloadEvent.Publish(7);

        await Task.WhenAll(parameterlessInvoked.Task, payloadInvoked.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, parameterlessActionCount);
        Assert.Equal(1, payloadFilterCount);
        Assert.Equal(1, payloadActionCount);
    }

    [Fact]
    public async Task SingleSendInvokesEachAsyncDelegateExactlyOnce()
    {
        var parameterlessActionCount = 0;
        var payloadFilterCount = 0;
        var payloadActionCount = 0;
        var parameterlessEvent = new AsyncPubSubEvent();
        var payloadEvent = new AsyncPubSubEvent<int>();
        parameterlessEvent.Subscribe(() =>
        {
            parameterlessActionCount++;
            return ValueTask.CompletedTask;
        }, true);
        payloadEvent.Subscribe(
            _ =>
            {
                payloadActionCount++;
                return ValueTask.CompletedTask;
            },
            ThreadOption.PublisherThread,
            true,
            _ =>
            {
                payloadFilterCount++;
                return true;
            });

        await parameterlessEvent.Send();
        await payloadEvent.Send(7);

        Assert.Equal(1, parameterlessActionCount);
        Assert.Equal(1, payloadFilterCount);
        Assert.Equal(1, payloadActionCount);
    }

    [Fact]
    public async Task SendAwaitsPublisherThreadCallbacksSequentially()
    {
        var pubSubEvent = new AsyncPubSubEvent();
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        pubSubEvent.Subscribe(async () =>
        {
            calls.Add("first-started");
            await releaseFirst.Task;
            calls.Add("first-completed");
        }, true);
        pubSubEvent.Subscribe(() =>
        {
            calls.Add("second");
            return ValueTask.CompletedTask;
        }, true);

        var send = pubSubEvent.Send();

        Assert.False(send.IsCompleted);
        Assert.Equal(new[] { "first-started" }, calls);

        releaseFirst.SetResult(true);
        await send;

        Assert.Equal(new[] { "first-started", "first-completed", "second" }, calls);
    }

    [Fact]
    public async Task GenericSendAwaitsCallbackAndDeliversTypedPayload()
    {
        var pubSubEvent = new AsyncPubSubEvent<int>();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedPayload = 0;
        var completed = false;
        pubSubEvent.Subscribe(async payload =>
        {
            receivedPayload = payload;
            await release.Task;
            completed = true;
        }, ThreadOption.PublisherThread, true, payload => payload > 0);

        var send = pubSubEvent.Send(37);

        Assert.Equal(37, receivedPayload);
        Assert.False(send.IsCompleted);
        Assert.False(completed);

        release.SetResult(true);
        await send;

        Assert.True(completed);
    }

    [Fact]
    public async Task SchedulerBackedSendWaitsForInvocationButNotCallbackValueTask()
    {
        var parameterlessEvent = new AsyncPubSubEvent();
        var payloadEvent = new AsyncPubSubEvent<int>();
        var callbackRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parameterlessStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var payloadStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        parameterlessEvent.Subscribe(() =>
        {
            parameterlessStarted.TrySetResult(true);
            return new ValueTask(callbackRelease.Task);
        }, ThreadOption.BackgroundThread, true);
        payloadEvent.Subscribe(payload =>
        {
            payloadStarted.TrySetResult(payload);
            return new ValueTask(callbackRelease.Task);
        }, ThreadOption.BackgroundThread, true, payload => payload == 11);

        await parameterlessEvent.Send();
        await payloadEvent.Send(11);

        Assert.True(await parameterlessStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(11, await payloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(callbackRelease.Task.IsCompleted);
        callbackRelease.SetResult(true);
    }

    [Fact]
    public async Task PublishReturnsBeforePublisherThreadCallbackCompletes()
    {
        var pubSubEvent = new AsyncPubSubEvent();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pubSubEvent.Subscribe(async () =>
        {
            started.TrySetResult(true);
            await release.Task;
            completed.TrySetResult(true);
        }, true);

        pubSubEvent.Publish();

        Assert.True(await started.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(completed.Task.IsCompleted);

        release.SetResult(true);
        Assert.True(await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task SendPropagatesPublisherThreadCallbackExceptions()
    {
        var parameterlessEvent = new AsyncPubSubEvent();
        var payloadEvent = new AsyncPubSubEvent<int>();
        parameterlessEvent.Subscribe(
            () => ValueTask.FromException(new InvalidOperationException("parameterless")),
            true);
        payloadEvent.Subscribe(
            _ => ValueTask.FromException(new InvalidOperationException("payload")),
            true);

        var parameterlessException = await Assert.ThrowsAsync<InvalidOperationException>(() => parameterlessEvent.Send().AsTask());
        var payloadException = await Assert.ThrowsAsync<InvalidOperationException>(() => payloadEvent.Send(1).AsTask());

        Assert.Equal("parameterless", parameterlessException.Message);
        Assert.Equal("payload", payloadException.Message);
    }

    [Fact]
    public async Task CompatibilityPublishInvokesParameterlessSubscriber()
    {
        var pubSubEvent = new ExposedAsyncPubSubEvent();
        var invoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pubSubEvent.Subscribe(() =>
        {
            invoked.TrySetResult(true);
            return ValueTask.CompletedTask;
        }, true);

        pubSubEvent.PublishCompatibility();

        Assert.True(await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task GenericCompatibilityPublishHandlesPayloadAndDefaultArguments()
    {
        var pubSubEvent = new ExposedAsyncPubSubEvent<int>();
        var payloads = new List<int>();
        var receivedTwice = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pubSubEvent.Subscribe(payload =>
        {
            payloads.Add(payload);
            if (payloads.Count == 2)
            {
                receivedTwice.TrySetResult(true);
            }

            return ValueTask.CompletedTask;
        }, true);

        pubSubEvent.PublishCompatibility(19);
        pubSubEvent.PublishCompatibility();

        Assert.True(await receivedTwice.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(new[] { 19, 0 }, payloads);
    }

    [Fact]
    public async Task EmptySendCompletesForBothEventShapes()
    {
        await new AsyncPubSubEvent().Send();
        await new AsyncPubSubEvent<int>().Send(1);
    }

    [Fact]
    public void SubscribeValidatesUiContextAndUsesPublisherFallbackForUnknownOption()
    {
        var parameterlessEvent = new ExposedAsyncPubSubEvent();
        var payloadEvent = new ExposedAsyncPubSubEvent<int>();
        Func<ValueTask> parameterlessAction = () => ValueTask.CompletedTask;
        Func<int, ValueTask> payloadAction = _ => ValueTask.CompletedTask;

        Assert.Throws<InvalidOperationException>(() => parameterlessEvent.Subscribe(parameterlessAction, ThreadOption.UIThread));
        Assert.Throws<InvalidOperationException>(() => payloadEvent.Subscribe(payloadAction, ThreadOption.UIThread));

        parameterlessEvent.Subscribe(parameterlessAction, (ThreadOption)(-1), true);
        payloadEvent.Subscribe(payloadAction, (ThreadOption)(-1), true);

        Assert.IsType<AsyncEventSubscription>(parameterlessEvent.SingleSubscription);
        Assert.IsType<AsyncEventSubscription<int>>(payloadEvent.SingleSubscription);
    }

    [Fact]
    public void GenericAndNonGenericEventsUseSeparateSubscriptionImplementations()
    {
        var parameterlessEvent = new ExposedAsyncPubSubEvent();
        var payloadEvent = new ExposedAsyncPubSubEvent<int>();
        parameterlessEvent.Subscribe(() => ValueTask.CompletedTask, true);
        payloadEvent.Subscribe(_ => ValueTask.CompletedTask, true);

        Assert.IsType<AsyncEventSubscription>(parameterlessEvent.SingleSubscription);
        Assert.IsType<AsyncEventSubscription<int>>(payloadEvent.SingleSubscription);
        Assert.NotEqual(parameterlessEvent.SingleSubscription.GetType(), payloadEvent.SingleSubscription.GetType());
    }

    [Fact]
    public void InternalSubscribeRejectsNullForBothEventShapes()
    {
        var parameterlessEvent = new ExposedAsyncPubSubEvent();
        var payloadEvent = new ExposedAsyncPubSubEvent<int>();

        Assert.Throws<ArgumentNullException>(() => parameterlessEvent.AddSubscription(null));
        Assert.Throws<ArgumentNullException>(() => payloadEvent.AddSubscription(null));
    }

    private sealed class ExposedAsyncPubSubEvent : AsyncPubSubEvent
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

    private sealed class ExposedAsyncPubSubEvent<TPayload> : AsyncPubSubEvent<TPayload>
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
}
