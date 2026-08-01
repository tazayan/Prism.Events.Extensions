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

            IDelegateReference actionDelegateReference = new MockDelegateReference() { Target = action };
            IDelegateReference filterDelegateReference = new MockDelegateReference() { Target = (Predicate<object>)delegate { return true; } };

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

            IDelegateReference actionDelegateReference = new MockDelegateReference() { Target = action };

            var eventSubscription = new BackgroundEventSubscription(actionDelegateReference);

            var publishAction = eventSubscription.Action;

            Assert.NotNull(publishAction);

            eventSubscription.InvokeAction();

            completeEvent.WaitOne(5000);

            Assert.NotEqual(SynchronizationContext.Current, calledSyncContext);
        }
    }
}
