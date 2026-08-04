using Xunit;

namespace Prism.Events.Extensions.Tests;

public class AsyncEventSubscriptionExtensionFixture
{
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
        }));
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
        }));
        var filterReference = new MockDelegateReference((Predicate<int>)(_ => true));
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
    public async Task DefaultSchedulerInvokesParameterlessActionWithoutAwaitingItsReturnedValueTask()
    {
        var callbackRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoked = false;
        var actionReference = new MockDelegateReference((Func<ValueTask>)(() =>
        {
            invoked = true;
            return new ValueTask(callbackRelease.Task);
        }));
        var subscription = TaskEventSubscription.CreateDefault(actionReference);

        await subscription.InvokeAction();

        Assert.True(invoked);
        Assert.False(callbackRelease.Task.IsCompleted);
        callbackRelease.SetResult(true);

        actionReference.Target = null;
        await subscription.InvokeAction();
    }

    [Fact]
    public async Task DefaultSchedulerAppliesPayloadFilterWithoutAwaitingReturnedValueTask()
    {
        var callbackRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var payloads = new List<int>();
        var actionReference = new MockDelegateReference((Func<int, ValueTask>)(payload =>
        {
            payloads.Add(payload);
            return new ValueTask(callbackRelease.Task);
        }));
        var filterReference = new MockDelegateReference((Predicate<int>)(payload => payload > 0));
        var subscription = TaskEventSubscription<int>.CreateDefault(actionReference, filterReference);

        await subscription.InvokeAction(7);
        await subscription.InvokeAction(-1);

        Assert.Equal(new[] { 7 }, payloads);
        Assert.False(callbackRelease.Task.IsCompleted);
        callbackRelease.SetResult(true);

        filterReference.Target = null;
        await subscription.InvokeAction(8);
        actionReference.Target = null;
        filterReference.Target = (Predicate<int>)(_ => true);
        await subscription.InvokeAction(9);
        Assert.Equal(new[] { 7 }, payloads);
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
