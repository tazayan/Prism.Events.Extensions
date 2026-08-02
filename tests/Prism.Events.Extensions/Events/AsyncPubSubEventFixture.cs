using Xunit;

namespace Prism.Events.Extensions.Tests
{
    public class AsyncPubSubEventFixture
    {
        [Fact]
        public void EnsureSubscriptionListIsEmptyAfterPublishingAMessage()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();
            SubscribeExternalActionWithoutReference(asyncPubSubEvent);
            GC.Collect();
            asyncPubSubEvent.Publish("testPayload");
            Assert.True(asyncPubSubEvent.BaseSubscriptions.Count == 0, "Subscriptionlist is not empty");
        }

        [Fact]
        public void EnsureSubscriptionListIsNotEmptyWithoutPublishOrSubscribe()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();
            SubscribeExternalActionWithoutReference(asyncPubSubEvent);
            GC.Collect();
            Assert.True(asyncPubSubEvent.BaseSubscriptions.Count == 1, "Subscriptionlist is empty");
        }

        [Fact]
        public void EnsureSubscriptionListIsEmptyAfterSubscribeAgainAMessage()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();
            SubscribeExternalActionWithoutReference(asyncPubSubEvent);
            GC.Collect();
            SubscribeExternalActionWithoutReference(asyncPubSubEvent);
            asyncPubSubEvent.Prune();
            Assert.True(asyncPubSubEvent.BaseSubscriptions.Count == 1, "Subscriptionlist is empty");
        }

        private static void SubscribeExternalActionWithoutReference(TestableAsyncPubSubEvent<string> asyncPubSubEvent)
        {
            asyncPubSubEvent.Subscribe(new AsyncExternalAction().ExecuteAction);
        }


        [Fact]
        public void CanSubscribeAndRaiseEvent()
        {
            TestableAsyncPubSubEvent<string> asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();
            bool published = false;
            asyncPubSubEvent.Subscribe(delegate { published = true; return ValueTask.CompletedTask; }, ThreadOption.PublisherThread, true, delegate { return true; });
            asyncPubSubEvent.Publish(null);

            Assert.True(published);
        }

        [Fact]
        public void CanSubscribeAndRaiseEventNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();
            bool published = false;
            asyncPubSubEvent.Subscribe(delegate { published = true; return ValueTask.CompletedTask; }, ThreadOption.PublisherThread, true);
            asyncPubSubEvent.Publish();

            Assert.True(published);
        }

        [Fact]
        public void CanSubscribeAndRaiseCustomEvent()
        {
            var customEvent = new TestableAsyncPubSubEvent<Payload>();
            Payload payload = new Payload();
            var action = new AsyncActionHelper();
            customEvent.Subscribe(action.Action);

            customEvent.Publish(payload);

            Assert.Same(action.ActionArg<Payload>(), payload);
        }

        [Fact]
        public void CanHaveMultipleSubscribersAndRaiseCustomEvent()
        {
            var customEvent = new TestableAsyncPubSubEvent<Payload>();
            Payload payload = new Payload();
            var action1 = new AsyncActionHelper();
            var action2 = new AsyncActionHelper();
            customEvent.Subscribe(action1.Action);
            customEvent.Subscribe(action2.Action);

            customEvent.Publish(payload);

            Assert.Same(action1.ActionArg<Payload>(), payload);
            Assert.Same(action2.ActionArg<Payload>(), payload);
        }

        [Fact]
        public void CanHaveMultipleSubscribersAndRaiseEvent()
        {
            var customEvent = new TestableAsyncPubSubEvent();
            var action1 = new AsyncActionHelper();
            var action2 = new AsyncActionHelper();
            customEvent.Subscribe(action1.Action);
            customEvent.Subscribe(action2.Action);

            customEvent.Publish();

            Assert.True(action1.ActionCalled);
            Assert.True(action2.ActionCalled);
        }

        [Fact]
        public void SubscribeTakesExecuteDelegateThreadOptionAndFilter()
        {
            TestableAsyncPubSubEvent<string> asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();
            var action = new AsyncActionHelper();
            asyncPubSubEvent.Subscribe(action.Action);

            asyncPubSubEvent.Publish("test");

            Assert.Equal("test", action.ActionArg<string>());

        }

        [Fact]
        public void FilterEnablesActionTarget()
        {
            TestableAsyncPubSubEvent<string> asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();
            var goodFilter = new MockFilter { FilterReturnValue = true };
            var actionGoodFilter = new AsyncActionHelper();
            var badFilter = new MockFilter { FilterReturnValue = false };
            var actionBadFilter = new AsyncActionHelper();
            asyncPubSubEvent.Subscribe(actionGoodFilter.Action, ThreadOption.PublisherThread, true, goodFilter.FilterString);
            asyncPubSubEvent.Subscribe(actionBadFilter.Action, ThreadOption.PublisherThread, true, badFilter.FilterString);

            asyncPubSubEvent.Publish("test");

            Assert.True(actionGoodFilter.ActionCalled);
            Assert.False(actionBadFilter.ActionCalled);

        }

        [Fact]
        public void FilterEnablesActionTarget_Weak()
        {
            TestableAsyncPubSubEvent<string> asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();
            var goodFilter = new MockFilter { FilterReturnValue = true };
            var actionGoodFilter = new AsyncActionHelper();
            var badFilter = new MockFilter { FilterReturnValue = false };
            var actionBadFilter = new AsyncActionHelper();
            asyncPubSubEvent.Subscribe(actionGoodFilter.Action, goodFilter.FilterString);
            asyncPubSubEvent.Subscribe(actionBadFilter.Action, badFilter.FilterString);

            asyncPubSubEvent.Publish("test");

            Assert.True(actionGoodFilter.ActionCalled);
            Assert.False(actionBadFilter.ActionCalled);

        }

        [Fact]
        public void SubscribeDefaultsThreadOptionAndNoFilter()
        {
            TestableAsyncPubSubEvent<string> asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            SynchronizationContext calledSyncContext = null;
            var myAction = new AsyncActionHelper()
            {
                ActionToExecute =
                      () =>
                      {
                          calledSyncContext = SynchronizationContext.Current;
                          return ValueTask.CompletedTask;
                      }
            };
            asyncPubSubEvent.Subscribe(myAction.Action);

            asyncPubSubEvent.Publish("test");

            Assert.Equal(SynchronizationContext.Current, calledSyncContext);
        }

        [Fact]
        public void SubscribeDefaultsThreadOptionAndNoFilterNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            SynchronizationContext calledSyncContext = null;
            var myAction = new AsyncActionHelper()
            {
                ActionToExecute =
                    () =>
                    {
                        calledSyncContext = SynchronizationContext.Current;
                        return ValueTask.CompletedTask;
                    }
            };
            asyncPubSubEvent.Subscribe(myAction.Action);

            asyncPubSubEvent.Publish();

            Assert.Equal(SynchronizationContext.Current, calledSyncContext);
        }

        [Fact]
        public void ShouldUnsubscribeFromPublisherThread()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();

            var actionEvent = new AsyncActionHelper();
            PubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.PublisherThread);

            Assert.True(PubSubEvent.Contains(actionEvent.Action));
            PubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(PubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void ShouldUnsubscribeFromPublisherThreadNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();

            var actionEvent = new AsyncActionHelper();
            asyncPubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.PublisherThread);

            Assert.True(asyncPubSubEvent.Contains(actionEvent.Action));
            asyncPubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(asyncPubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void UnsubscribeShouldNotFailWithNonSubscriber()
        {
            TestableAsyncPubSubEvent<string> asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();

            Func<string, ValueTask> subscriber = delegate { return ValueTask.CompletedTask; };
            asyncPubSubEvent.Unsubscribe(subscriber);
        }

        [Fact]
        public void UnsubscribeShouldNotFailWithNonSubscriberNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();

            Func<ValueTask> subscriber = delegate { return ValueTask.CompletedTask; };
            asyncPubSubEvent.Unsubscribe(subscriber);
        }

        [Fact]
        public void ShouldUnsubscribeFromBackgroundThread()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();

            var actionEvent = new AsyncActionHelper();
            PubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.BackgroundThread);

            Assert.True(PubSubEvent.Contains(actionEvent.Action));
            PubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(PubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void ShouldUnsubscribeFromBackgroundThreadNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();

            var actionEvent = new AsyncActionHelper();
            asyncPubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.BackgroundThread);

            Assert.True(asyncPubSubEvent.Contains(actionEvent.Action));
            asyncPubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(asyncPubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void ShouldUnsubscribeFromUIThread()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();
            PubSubEvent.SynchronizationContext = new SynchronizationContext();

            var actionEvent = new AsyncActionHelper();
            PubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.UIThread);

            Assert.True(PubSubEvent.Contains(actionEvent.Action));
            PubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(PubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void ShouldUnsubscribeFromUIThreadNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();
            asyncPubSubEvent.SynchronizationContext = new SynchronizationContext();

            var actionEvent = new AsyncActionHelper();
            asyncPubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.UIThread);

            Assert.True(asyncPubSubEvent.Contains(actionEvent.Action));
            asyncPubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(asyncPubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void ShouldUnsubscribeASingleDelegate()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();

            int callCount = 0;

            var actionEvent = new AsyncActionHelper()
            {
                ActionToExecute = () =>
                {
                    callCount++; return ValueTask.CompletedTask;
                }
            };
            PubSubEvent.Subscribe(actionEvent.Action);
            PubSubEvent.Subscribe(actionEvent.Action);

            PubSubEvent.Publish(null);
            Assert.Equal<int>(2, callCount);

            callCount = 0;
            PubSubEvent.Unsubscribe(actionEvent.Action);
            PubSubEvent.Publish(null);
            Assert.Equal<int>(1, callCount);
        }

        [Fact]
        public void ShouldUnsubscribeASingleDelegateNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();

            int callCount = 0;

            var actionEvent = new AsyncActionHelper()
            {
                ActionToExecute = () =>
                {
                    callCount++; return ValueTask.CompletedTask;
                }
            };
            asyncPubSubEvent.Subscribe(actionEvent.Action);
            asyncPubSubEvent.Subscribe(actionEvent.Action);

            asyncPubSubEvent.Publish();
            Assert.Equal<int>(2, callCount);

            callCount = 0;
            asyncPubSubEvent.Unsubscribe(actionEvent.Action);
            asyncPubSubEvent.Publish();
            Assert.Equal<int>(1, callCount);
        }

        [Fact]
        public async Task ShouldNotExecuteOnGarbageCollectedDelegateReferenceWhenNotKeepAlive()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();

            AsyncExternalAction externalAction = new AsyncExternalAction();
            PubSubEvent.Subscribe(externalAction.ExecuteAction);

            PubSubEvent.Publish("testPayload");
            Assert.Equal("testPayload", externalAction.PassedValue);

            WeakReference actionEventReference = new WeakReference(externalAction);
            externalAction = null;
            await Task.Delay(100);
            GC.Collect();
            Assert.False(actionEventReference.IsAlive);

            PubSubEvent.Publish("testPayload");
        }

        [Fact]
        public async Task ShouldNotExecuteOnGarbageCollectedDelegateReferenceWhenNotKeepAliveNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();

            var externalAction = new AsyncExternalAction();
            asyncPubSubEvent.Subscribe(externalAction.ExecuteAction);

            asyncPubSubEvent.Publish();
            Assert.True(externalAction.Executed);

            var actionEventReference = new WeakReference(externalAction);
            externalAction = null;
            await Task.Delay(100);
            GC.Collect();
            Assert.False(actionEventReference.IsAlive);

            asyncPubSubEvent.Publish();
        }

        [Fact]
        public async Task ShouldNotExecuteOnGarbageCollectedFilterReferenceWhenNotKeepAlive()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();

            bool wasCalled = false;
            var actionEvent = new AsyncActionHelper() { ActionToExecute = () => { wasCalled = true; return ValueTask.CompletedTask; } };

            ExternalFilter filter = new ExternalFilter();
            PubSubEvent.Subscribe(actionEvent.Action, ThreadOption.PublisherThread, false, filter.AlwaysTrueFilter);

            PubSubEvent.Publish("testPayload");
            Assert.True(wasCalled);

            wasCalled = false;
            WeakReference filterReference = new WeakReference(filter);
            filter = null;
            await Task.Delay(100);
            GC.Collect();
            Assert.False(filterReference.IsAlive);

            PubSubEvent.Publish("testPayload");
            Assert.False(wasCalled);
        }

        [Fact]
        public void CanAddSubscriptionWhileEventIsFiring()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent<string>();

            var emptyAction = new AsyncActionHelper();
            var subscriptionAction = new AsyncActionHelper
            {
                ActionToExecute = (() =>
                {
                    asyncPubSubEvent.Subscribe(emptyAction.Action);
                    return ValueTask.CompletedTask;
                })
            };

            asyncPubSubEvent.Subscribe(subscriptionAction.Action);

            Assert.False(asyncPubSubEvent.Contains(emptyAction.Action));

            asyncPubSubEvent.Publish(null);

            Assert.True((asyncPubSubEvent.Contains(emptyAction.Action)));
        }

        [Fact]
        public void CanAddSubscriptionWhileEventIsFiringNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();

            var emptyAction = new AsyncActionHelper();
            var subscriptionAction = new AsyncActionHelper
            {
                ActionToExecute = (() =>
                {
                    asyncPubSubEvent.Subscribe(emptyAction.Action);
                    return ValueTask.CompletedTask;
                })
            };

            asyncPubSubEvent.Subscribe(subscriptionAction.Action);

            Assert.False(asyncPubSubEvent.Contains(emptyAction.Action));

            asyncPubSubEvent.Publish();

            Assert.True((asyncPubSubEvent.Contains(emptyAction.Action)));
        }

        [Fact]
        public void InlineDelegateDeclarationsDoesNotGetCollectedIncorrectlyWithWeakReferences()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();
            bool published = false;
            PubSubEvent.Subscribe(delegate { published = true; return ValueTask.CompletedTask; }, ThreadOption.PublisherThread, false, delegate { return true; });
            GC.Collect();
            PubSubEvent.Publish(null);

            Assert.True(published);
        }

        [Fact]
        public void InlineDelegateDeclarationsDoesNotGetCollectedIncorrectlyWithWeakReferencesNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();
            bool published = false;
            asyncPubSubEvent.Subscribe(delegate { published = true; return ValueTask.CompletedTask; }, ThreadOption.PublisherThread, false);
            GC.Collect();
            asyncPubSubEvent.Publish();

            Assert.True(published);
        }

        [Fact]
        public void ShouldNotGarbageCollectDelegateReferenceWhenUsingKeepAlive()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();

            var externalAction = new AsyncExternalAction();
            PubSubEvent.Subscribe(externalAction.ExecuteAction, ThreadOption.PublisherThread, true);

            WeakReference actionEventReference = new WeakReference(externalAction);
            externalAction = null;
            GC.Collect();
            GC.Collect();
            Assert.True(actionEventReference.IsAlive);

            PubSubEvent.Publish("testPayload");

            Assert.Equal("testPayload", ((AsyncExternalAction)actionEventReference.Target).PassedValue);
        }

        [Fact]
        public void ShouldNotGarbageCollectDelegateReferenceWhenUsingKeepAliveNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();

            var externalAction = new AsyncExternalAction();
            asyncPubSubEvent.Subscribe(externalAction.ExecuteAction, ThreadOption.PublisherThread, true);

            WeakReference actionEventReference = new WeakReference(externalAction);
            externalAction = null;
            GC.Collect();
            GC.Collect();
            Assert.True(actionEventReference.IsAlive);

            asyncPubSubEvent.Publish();

            Assert.True(((AsyncExternalAction)actionEventReference.Target).Executed);
        }

        [Fact]
        public void RegisterReturnsTokenThatCanBeUsedToUnsubscribe()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();
            var emptyAction = new AsyncActionHelper();

            var token = PubSubEvent.Subscribe(emptyAction.Action);
            PubSubEvent.Unsubscribe(token);

            Assert.False(PubSubEvent.Contains(emptyAction.Action));
        }

        [Fact]
        public void RegisterReturnsTokenThatCanBeUsedToUnsubscribeNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();
            var emptyAction = new AsyncActionHelper();

            var token = asyncPubSubEvent.Subscribe(emptyAction.Action);
            asyncPubSubEvent.Unsubscribe(token);

            Assert.False(asyncPubSubEvent.Contains(emptyAction.Action));
        }

        [Fact]
        public void ContainsShouldSearchByToken()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();
            var emptyAction = new AsyncActionHelper();
            var token = PubSubEvent.Subscribe(emptyAction.Action);

            Assert.True(PubSubEvent.Contains(token));

            PubSubEvent.Unsubscribe(emptyAction.Action);
            Assert.False(PubSubEvent.Contains(token));
        }

        [Fact]
        public void ContainsShouldSearchByTokenNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();
            var emptyAction = new AsyncActionHelper();
            var token = asyncPubSubEvent.Subscribe(emptyAction.Action);

            Assert.True(asyncPubSubEvent.Contains(token));

            asyncPubSubEvent.Unsubscribe(emptyAction.Action);
            Assert.False(asyncPubSubEvent.Contains(token));
        }

        [Fact]
        public void SubscribeDefaultsToPublisherThread()
        {
            var PubSubEvent = new TestableAsyncPubSubEvent<string>();
            Func<string, ValueTask> action = delegate { return ValueTask.CompletedTask; };
            var token = PubSubEvent.Subscribe(action, true);

            Assert.Single(PubSubEvent.BaseSubscriptions);
            Assert.Equal(typeof(AsyncEventSubscription<string>), PubSubEvent.BaseSubscriptions.ElementAt(0).GetType());
        }

        [Fact]
        public void SubscribeDefaultsToPublisherThreadNonGeneric()
        {
            var asyncPubSubEvent = new TestableAsyncPubSubEvent();
            Func<ValueTask> action = delegate { return ValueTask.CompletedTask; };
            var token = asyncPubSubEvent.Subscribe(action, true);

            Assert.Single(asyncPubSubEvent.BaseSubscriptions);
            Assert.Equal(typeof(AsyncEventSubscription), asyncPubSubEvent.BaseSubscriptions.ElementAt(0).GetType());
        }

        public class ExternalFilter
        {
            public bool AlwaysTrueFilter(string value)
            {
                return true;
            }
        }

        public class AsyncExternalAction
        {
            public string PassedValue;
            public bool Executed = false;

            public ValueTask ExecuteAction(string value)
            {
                PassedValue = value;
                Executed = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask ExecuteAction()
            {
                Executed = true;
                return ValueTask.CompletedTask;
            }
        }

        class TestableAsyncPubSubEvent<TPayload> : AsyncPubSubEvent<TPayload>
        {
            public ICollection<IEventSubscription> BaseSubscriptions
            {
                get { return base.Subscriptions; }
            }
        }

        class TestableAsyncPubSubEvent : AsyncPubSubEvent
        {
            public ICollection<IEventSubscription> BaseSubscriptions
            {
                get { return base.Subscriptions; }
            }
        }

        public class Payload { }
    }

    public class AsyncActionHelper
    {
        public bool ActionCalled;
        public Func<ValueTask> ActionToExecute = null;
        private object actionArg;

        public T ActionArg<T>()
        {
            return (T)actionArg;
        }

        public ValueTask Action(AsyncPubSubEventFixture.Payload arg)
        {
            Action((object)arg);
            return ValueTask.CompletedTask;
        }

        public ValueTask Action(string arg)
        {
            Action((object)arg);
            return ValueTask.CompletedTask;
        }

        public ValueTask Action(object arg)
        {
            actionArg = arg;
            ActionCalled = true;
            if (ActionToExecute != null)
            {
                return ActionToExecute.Invoke();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask Action()
        {
            ActionCalled = true;
            if (ActionToExecute != null)
            {
                return ActionToExecute.Invoke();
            }

            return ValueTask.CompletedTask;
        }
    }
}
