using Xunit;

namespace Prism.Events.Extensions.Tests;

public class AsyncEventSubscriptionExtensionFixture
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void ParameterlessSubscriptionRejectsInvalidActionReferences()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncEventSubscription(null));
        Assert.Throws<ArgumentException>(() => new AsyncEventSubscription(new MockDelegateReference((Action)(() => { }))));
    }

    [Fact]
    public void PayloadSubscriptionRejectsInvalidDelegateReferences()
    {
        var action = new MockDelegateReference((Func<int, ValueTask>)(_ => ValueTask.CompletedTask));
        var filter = new MockDelegateReference((Predicate<int>)(_ => true));

        Assert.Throws<ArgumentNullException>(() => new AsyncEventSubscription<int>(null, filter));
        Assert.Throws<ArgumentException>(() => new AsyncEventSubscription<int>(new MockDelegateReference((Action<int>)(_ => { })), filter));
        Assert.Throws<ArgumentNullException>(() => new AsyncEventSubscription<int>(action, null));
        Assert.Throws<ArgumentException>(() => new AsyncEventSubscription<int>(action, new MockDelegateReference((Predicate<string>)(_ => true))));
    }

    [Fact]
    public async Task ParameterlessCompatibilityStrategyInvokesLiveActionAndPrunesDeadAction()
    {
        var invocationCount = 0;
        var actionReference = new MockDelegateReference((Func<ValueTask>)(() =>
        {
            invocationCount++;
            return ValueTask.CompletedTask;
        }), preventTargetSet: false);
        var subscription = new AsyncEventSubscription(actionReference);

        var strategy = ((IEventSubscription)subscription).GetExecutionStrategy();

        Assert.NotNull(strategy);
        strategy(Array.Empty<object>());
        Assert.Equal(1, invocationCount);
        Assert.True(subscription.IsSubscriptionAlive());

        actionReference.Target = null;

        Assert.Null(((IEventSubscription)subscription).GetExecutionStrategy());
        Assert.False(subscription.IsSubscriptionAlive());
        await subscription.InvokeAction();
        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void PayloadCompatibilityStrategyHandlesPayloadAndDefaultArgumentShapes()
    {
        var payloads = new List<int>();
        var actionReference = new MockDelegateReference((Func<int, ValueTask>)(payload =>
        {
            payloads.Add(payload);
            return ValueTask.CompletedTask;
        }));
        var filterReference = new MockDelegateReference((Predicate<int>)(_ => true));
        var subscription = new AsyncEventSubscription<int>(actionReference, filterReference);
        var strategy = ((IEventSubscription)subscription).GetExecutionStrategy();

        Assert.NotNull(strategy);
        strategy(new object[] { 42 });
        strategy(null);
        strategy(Array.Empty<object>());
        strategy(new object[] { null });

        Assert.Equal(new[] { 42, 0, 0, 0 }, payloads);
    }

    [Fact]
    public async Task PayloadSubscriptionRequiresBothActionAndFilterToRemainAlive()
    {
        var invocationCount = 0;
        var actionReference = new MockDelegateReference((Func<int, ValueTask>)(_ =>
        {
            invocationCount++;
            return ValueTask.CompletedTask;
        }), preventTargetSet: false);
        var filterReference = new MockDelegateReference((Predicate<int>)(_ => true), preventTargetSet: false);
        var subscription = new AsyncEventSubscription<int>(actionReference, filterReference);

        Assert.True(subscription.IsSubscriptionAlive());

        filterReference.Target = null;

        Assert.False(subscription.IsSubscriptionAlive());
        Assert.Null(((IEventSubscription)subscription).GetExecutionStrategy());
        await subscription.InvokeAction(1);
        Assert.Equal(0, invocationCount);

        filterReference.Target = (Predicate<int>)(_ => true);
        actionReference.Target = null;

        Assert.False(subscription.IsSubscriptionAlive());
        await subscription.InvokeAction(1);
        Assert.Equal(0, invocationCount);
    }

    [Fact]
    public async Task DefaultSchedulerCompletesOnlyAfterParameterlessCallbackValueTaskCompletes()
    {
        var callbackRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var actionReference = new MockDelegateReference((Func<ValueTask>)(() =>
        {
            started.TrySetResult(Environment.CurrentManagedThreadId);
            return new ValueTask(callbackRelease.Task);
        }), preventTargetSet: false);
        var subscription = TaskEventSubscription.CreateDefault(actionReference);

        var invocation = subscription.InvokeAction().AsTask();

        await started.Task.WaitAsync(TestTimeout);
        Assert.False(invocation.IsCompleted);
        callbackRelease.SetResult(true);
        await invocation.WaitAsync(TestTimeout);

        actionReference.Target = null;
        Assert.True(subscription.InvokeAction().IsCompletedSuccessfully);
    }

    [Fact]
    public async Task DefaultSchedulerAppliesPayloadFilterAndAwaitsCallbackValueTask()
    {
        var callbackRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var payloads = new List<int>();
        var actionReference = new MockDelegateReference((Func<int, ValueTask>)(payload =>
        {
            lock (payloads) payloads.Add(payload);
            return new ValueTask(callbackRelease.Task);
        }), preventTargetSet: false);
        var filterReference = new MockDelegateReference((Predicate<int>)(payload => payload > 0), preventTargetSet: false);
        var subscription = TaskEventSubscription<int>.CreateDefault(actionReference, filterReference);

        await subscription.InvokeAction(-1).AsTask().WaitAsync(TestTimeout);
        var accepted = subscription.InvokeAction(7).AsTask();
        await Task.Delay(50);

        Assert.Equal(new[] { 7 }, payloads);
        Assert.False(accepted.IsCompleted);
        callbackRelease.SetResult(true);
        await accepted.WaitAsync(TestTimeout);

        filterReference.Target = null;
        Assert.True(subscription.InvokeAction(8).IsCompletedSuccessfully);
        actionReference.Target = null;
        filterReference.Target = (Predicate<int>)(_ => true);
        Assert.True(subscription.InvokeAction(9).IsCompletedSuccessfully);
        Assert.Equal(new[] { 7 }, payloads);
    }

    [Fact]
    public async Task DefaultSchedulerFaultsWhenCallbackOrFilterThrows()
    {
        var parameterless = TaskEventSubscription.CreateDefault(
            new MockDelegateReference((Func<ValueTask>)(() => throw new InvalidOperationException("sync"))));
        var asyncFault = TaskEventSubscription.CreateDefault(
            new MockDelegateReference((Func<ValueTask>)(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async");
            })));
        var filterFault = TaskEventSubscription<int>.CreateDefault(
            new MockDelegateReference((Func<int, ValueTask>)(_ => ValueTask.CompletedTask)),
            new MockDelegateReference((Predicate<int>)(_ => throw new InvalidOperationException("filter"))));

        Assert.Equal("sync", (await Assert.ThrowsAsync<InvalidOperationException>(() => parameterless.InvokeAction().AsTask())).Message);
        Assert.Equal("async", (await Assert.ThrowsAsync<InvalidOperationException>(() => asyncFault.InvokeAction().AsTask())).Message);
        Assert.Equal("filter", (await Assert.ThrowsAsync<InvalidOperationException>(() => filterFault.InvokeAction(1).AsTask())).Message);
    }

    [Fact]
    public void SynchronizationContextFactoriesRejectNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => TaskEventSubscription.CreateForSynchronizationContext(
            new MockDelegateReference((Func<ValueTask>)(() => ValueTask.CompletedTask)), null));
        Assert.Throws<ArgumentNullException>(() => TaskEventSubscription<int>.CreateForSynchronizationContext(
            new MockDelegateReference((Func<int, ValueTask>)(_ => ValueTask.CompletedTask)),
            new MockDelegateReference((Predicate<int>)(_ => true)),
            null));
    }

    [Fact]
    public async Task SynchronizationContextSchedulerRunsBothSubscriptionShapes()
    {
        var context = new ExecutingSynchronizationContext();
        var parameterlessInvoked = false;
        var payload = 0;
        var parameterless = TaskEventSubscription.CreateForSynchronizationContext(
            new MockDelegateReference((Func<ValueTask>)(() =>
            {
                parameterlessInvoked = true;
                return ValueTask.CompletedTask;
            })),
            context);
        var generic = TaskEventSubscription<int>.CreateForSynchronizationContext(
            new MockDelegateReference((Func<int, ValueTask>)(value =>
            {
                payload = value;
                return ValueTask.CompletedTask;
            })),
            new MockDelegateReference((Predicate<int>)(_ => true)),
            context);

        await parameterless.InvokeAction();
        await generic.InvokeAction(23);

        Assert.True(parameterlessInvoked);
        Assert.Equal(23, payload);
        Assert.True(context.PostCount >= 2);
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
