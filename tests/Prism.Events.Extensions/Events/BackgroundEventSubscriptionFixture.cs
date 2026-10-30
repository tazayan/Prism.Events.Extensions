using Xunit;

namespace Prism.Events.Extensions.Tests
{
    public class BackgroundEventSubscriptionFixture
    {
        [Fact]
        public void ShouldReceiveDelegateOnDifferentThread()
        {
            ManualResetEvent completeEvent = new ManualResetEvent(false);
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            SynchronizationContext calledSyncContext = null;
            Action<object> action = delegate
            {
                calledSyncContext = SynchronizationContext.Current;
                completeEvent.Set();
            };

            MockDelegateReference actionDelegateReference = new MockDelegateReference(action);
            MockDelegateReference filterDelegateReference = new MockDelegateReference((Predicate<object>)delegate { return true; } );

            var eventSubscription = new BackgroundEventSubscription<object>(actionDelegateReference, filterDelegateReference);


            var publishAction = eventSubscription.Action;

            Assert.NotNull(publishAction);

            var filterAction = eventSubscription.Filter;

            Assert.NotNull(filterAction);

            eventSubscription.InvokeAction(null);

            completeEvent.WaitOne(5000);

            Assert.NotEqual(SynchronizationContext.Current, calledSyncContext);
        }

        [Fact]
        public void ShouldReceiveDelegateOnDifferentThreadNonGeneric()
        {
            var completeEvent = new ManualResetEvent(false);
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            SynchronizationContext calledSyncContext = null;
            Action action = delegate
            {
                calledSyncContext = SynchronizationContext.Current;
                completeEvent.Set();
            };

            MockDelegateReference actionDelegateReference = new MockDelegateReference(action);

            var eventSubscription = new BackgroundEventSubscription(actionDelegateReference);

            var publishAction = eventSubscription.Action;

            Assert.NotNull(publishAction);

            eventSubscription.InvokeAction();

            completeEvent.WaitOne(5000);

            Assert.NotEqual(SynchronizationContext.Current, calledSyncContext);
        }
    }
}
