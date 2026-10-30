using Xunit;

namespace Prism.Events.Extensions.Tests
{
    public class DispatcherEventSubscriptionFixture
    {
        [Fact]
        public void ShouldCallInvokeOnDispatcher()
        {
            DispatcherEventSubscription<object> eventSubscription = null;

            MockDelegateReference actionDelegateReference = new MockDelegateReference(
                (Action<object>)(arg =>
                {
                    return;
                })
            );

            MockDelegateReference filterDelegateReference = new MockDelegateReference((Predicate<object>)(arg => true));
            var mockSyncContext = new MockSynchronizationContext();

            eventSubscription = new DispatcherEventSubscription<object>(actionDelegateReference, filterDelegateReference, mockSyncContext);

            eventSubscription.InvokeAction(new object[0]);

            Assert.True(mockSyncContext.InvokeCalled);
        }

        [Fact]
        public void ShouldCallInvokeOnDispatcherNonGeneric()
        {
            DispatcherEventSubscription eventSubscription = null;

            MockDelegateReference actionDelegateReference = new MockDelegateReference((Action)(() =>
            { }));

            var mockSyncContext = new MockSynchronizationContext();

            eventSubscription = new DispatcherEventSubscription(actionDelegateReference, mockSyncContext);

            eventSubscription.InvokeAction();

            Assert.True(mockSyncContext.InvokeCalled);
        }
    }

    internal class MockSynchronizationContext : SynchronizationContext
    {
        public bool InvokeCalled;
        public object InvokeArg;

        public override void Post(SendOrPostCallback d, object state)
        {
            InvokeCalled = true;
            InvokeArg = state;
        }
    }
}
