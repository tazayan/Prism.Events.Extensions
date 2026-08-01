using Xunit;

namespace Prism.Events.Extensions.Tests
{
    public class LightweightPubSubEventFixture
    {
        [Fact]
        public void EnsureSubscriptionListIsEmptyAfterPublishingAMessage()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();
            SubscribeExternalActionWithoutReference(lightweightPubSubEvent);
            GC.Collect();
            lightweightPubSubEvent.Publish("testPayload");
            Assert.True(lightweightPubSubEvent.BaseSubscriptions.Count == 0, "Subscriptionlist is not empty");
        }

        [Fact]
        public void EnsureSubscriptionListIsNotEmptyWithoutPublishOrSubscribe()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();
            SubscribeExternalActionWithoutReference(lightweightPubSubEvent);
            GC.Collect();
            Assert.True(lightweightPubSubEvent.BaseSubscriptions.Count == 1, "Subscriptionlist is empty");
        }

        [Fact]
        public void EnsureSubscriptionListIsEmptyAfterSubscribeAgainAMessage()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();
            SubscribeExternalActionWithoutReference(lightweightPubSubEvent);
            GC.Collect();
            SubscribeExternalActionWithoutReference(lightweightPubSubEvent);
            lightweightPubSubEvent.Prune();
            Assert.True(lightweightPubSubEvent.BaseSubscriptions.Count == 1, "Subscriptionlist is empty");
        }

        private static void SubscribeExternalActionWithoutReference(TestableLightweightPubSubEvent<string> lightweightPubSubEvent)
        {
            lightweightPubSubEvent.Subscribe(new ExternalAction().ExecuteAction);
        }


        [Fact]
        public void CanSubscribeAndRaiseEvent()
        {
            TestableLightweightPubSubEvent<string> lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();
            bool published = false;
            lightweightPubSubEvent.Subscribe(delegate { published = true; }, ThreadOption.PublisherThread, true, delegate { return true; });
            lightweightPubSubEvent.Publish(null);

            Assert.True(published);
        }

        [Fact]
        public void CanSubscribeAndRaiseEventNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();
            bool published = false;
            lightweightPubSubEvent.Subscribe(delegate { published = true; }, ThreadOption.PublisherThread, true);
            lightweightPubSubEvent.Publish();

            Assert.True(published);
        }

        [Fact]
        public void CanSubscribeAndRaiseCustomEvent()
        {
            var customEvent = new TestableLightweightPubSubEvent<Payload>();
            Payload payload = new Payload();
            var action = new ActionHelper();
            customEvent.Subscribe(action.Action);

            customEvent.Publish(payload);

            Assert.Same(action.ActionArg<Payload>(), payload);
        }

        [Fact]
        public void CanHaveMultipleSubscribersAndRaiseCustomEvent()
        {
            var customEvent = new TestableLightweightPubSubEvent<Payload>();
            Payload payload = new Payload();
            var action1 = new ActionHelper();
            var action2 = new ActionHelper();
            customEvent.Subscribe(action1.Action);
            customEvent.Subscribe(action2.Action);

            customEvent.Publish(payload);

            Assert.Same(action1.ActionArg<Payload>(), payload);
            Assert.Same(action2.ActionArg<Payload>(), payload);
        }

        [Fact]
        public void CanHaveMultipleSubscribersAndRaiseEvent()
        {
            var customEvent = new TestableLightweightPubSubEvent();
            var action1 = new ActionHelper();
            var action2 = new ActionHelper();
            customEvent.Subscribe(action1.Action);
            customEvent.Subscribe(action2.Action);

            customEvent.Publish();

            Assert.True(action1.ActionCalled);
            Assert.True(action2.ActionCalled);
        }

        [Fact]
        public void SubscribeTakesExecuteDelegateThreadOptionAndFilter()
        {
            TestableLightweightPubSubEvent<string> lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();
            var action = new ActionHelper();
            lightweightPubSubEvent.Subscribe(action.Action);

            lightweightPubSubEvent.Publish("test");

            Assert.Equal("test", action.ActionArg<string>());

        }

        [Fact]
        public void FilterEnablesActionTarget()
        {
            TestableLightweightPubSubEvent<string> lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();
            var goodFilter = new MockFilter { FilterReturnValue = true };
            var actionGoodFilter = new ActionHelper();
            var badFilter = new MockFilter { FilterReturnValue = false };
            var actionBadFilter = new ActionHelper();
            lightweightPubSubEvent.Subscribe(actionGoodFilter.Action, ThreadOption.PublisherThread, true, goodFilter.FilterString);
            lightweightPubSubEvent.Subscribe(actionBadFilter.Action, ThreadOption.PublisherThread, true, badFilter.FilterString);

            lightweightPubSubEvent.Publish("test");

            Assert.True(actionGoodFilter.ActionCalled);
            Assert.False(actionBadFilter.ActionCalled);

        }

        [Fact]
        public void FilterEnablesActionTarget_Weak()
        {
            TestableLightweightPubSubEvent<string> lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();
            var goodFilter = new MockFilter { FilterReturnValue = true };
            var actionGoodFilter = new ActionHelper();
            var badFilter = new MockFilter { FilterReturnValue = false };
            var actionBadFilter = new ActionHelper();
            lightweightPubSubEvent.Subscribe(actionGoodFilter.Action, goodFilter.FilterString);
            lightweightPubSubEvent.Subscribe(actionBadFilter.Action, badFilter.FilterString);

            lightweightPubSubEvent.Publish("test");

            Assert.True(actionGoodFilter.ActionCalled);
            Assert.False(actionBadFilter.ActionCalled);

        }

        [Fact]
        public void SubscribeDefaultsThreadOptionAndNoFilter()
        {
            TestableLightweightPubSubEvent<string> lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            SynchronizationContext calledSyncContext = null;
            var myAction = new ActionHelper()
            {
                ActionToExecute =
                    () => calledSyncContext = SynchronizationContext.Current
            };
            lightweightPubSubEvent.Subscribe(myAction.Action);

            lightweightPubSubEvent.Publish("test");

            Assert.Equal(SynchronizationContext.Current, calledSyncContext);
        }

        [Fact]
        public void SubscribeDefaultsThreadOptionAndNoFilterNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            SynchronizationContext calledSyncContext = null;
            var myAction = new ActionHelper()
            {
                ActionToExecute =
                    () => calledSyncContext = SynchronizationContext.Current
            };
            lightweightPubSubEvent.Subscribe(myAction.Action);

            lightweightPubSubEvent.Publish();

            Assert.Equal(SynchronizationContext.Current, calledSyncContext);
        }

        [Fact]
        public void ShouldUnsubscribeFromPublisherThread()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();

            var actionEvent = new ActionHelper();
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
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();

            var actionEvent = new ActionHelper();
            lightweightPubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.PublisherThread);

            Assert.True(lightweightPubSubEvent.Contains(actionEvent.Action));
            lightweightPubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(lightweightPubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void UnsubscribeShouldNotFailWithNonSubscriber()
        {
            TestableLightweightPubSubEvent<string> lightweightPubSubEvent = new TestableLightweightPubSubEvent<string>();

            Action<string> subscriber = delegate { };
            lightweightPubSubEvent.Unsubscribe(subscriber);
        }

        [Fact]
        public void UnsubscribeShouldNotFailWithNonSubscriberNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();

            Action subscriber = delegate { };
            lightweightPubSubEvent.Unsubscribe(subscriber);
        }

        [Fact]
        public void ShouldUnsubscribeFromBackgroundThread()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();

            var actionEvent = new ActionHelper();
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
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();

            var actionEvent = new ActionHelper();
            lightweightPubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.BackgroundThread);

            Assert.True(lightweightPubSubEvent.Contains(actionEvent.Action));
            lightweightPubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(lightweightPubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void ShouldUnsubscribeFromUIThread()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();
            PubSubEvent.SynchronizationContext = new SynchronizationContext();

            var actionEvent = new ActionHelper();
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
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();
            lightweightPubSubEvent.SynchronizationContext = new SynchronizationContext();

            var actionEvent = new ActionHelper();
            lightweightPubSubEvent.Subscribe(
                actionEvent.Action,
                ThreadOption.UIThread);

            Assert.True(lightweightPubSubEvent.Contains(actionEvent.Action));
            lightweightPubSubEvent.Unsubscribe(actionEvent.Action);
            Assert.False(lightweightPubSubEvent.Contains(actionEvent.Action));
        }

        [Fact]
        public void ShouldUnsubscribeASingleDelegate()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();

            int callCount = 0;

            var actionEvent = new ActionHelper() { ActionToExecute = () => callCount++ };
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
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();

            int callCount = 0;

            var actionEvent = new ActionHelper() { ActionToExecute = () => callCount++ };
            lightweightPubSubEvent.Subscribe(actionEvent.Action);
            lightweightPubSubEvent.Subscribe(actionEvent.Action);

            lightweightPubSubEvent.Publish();
            Assert.Equal<int>(2, callCount);

            callCount = 0;
            lightweightPubSubEvent.Unsubscribe(actionEvent.Action);
            lightweightPubSubEvent.Publish();
            Assert.Equal<int>(1, callCount);
        }

        [Fact]
        public async Task ShouldNotExecuteOnGarbageCollectedDelegateReferenceWhenNotKeepAlive()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();

            ExternalAction externalAction = new ExternalAction();
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
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();

            var externalAction = new ExternalAction();
            lightweightPubSubEvent.Subscribe(externalAction.ExecuteAction);

            lightweightPubSubEvent.Publish();
            Assert.True(externalAction.Executed);

            var actionEventReference = new WeakReference(externalAction);
            externalAction = null;
            await Task.Delay(100);
            GC.Collect();
            Assert.False(actionEventReference.IsAlive);

            lightweightPubSubEvent.Publish();
        }

        [Fact]
        public async Task ShouldNotExecuteOnGarbageCollectedFilterReferenceWhenNotKeepAlive()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();

            bool wasCalled = false;
            var actionEvent = new ActionHelper() { ActionToExecute = () => wasCalled = true };

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
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();

            var emptyAction = new ActionHelper();
            var subscriptionAction = new ActionHelper
            {
                ActionToExecute = (() =>
                                                  PubSubEvent.Subscribe(
                                                      emptyAction.Action))
            };

            PubSubEvent.Subscribe(subscriptionAction.Action);

            Assert.False(PubSubEvent.Contains(emptyAction.Action));

            PubSubEvent.Publish(null);

            Assert.True((PubSubEvent.Contains(emptyAction.Action)));
        }

        [Fact]
        public void CanAddSubscriptionWhileEventIsFiringNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();

            var emptyAction = new ActionHelper();
            var subscriptionAction = new ActionHelper
            {
                ActionToExecute = (() =>
                                          lightweightPubSubEvent.Subscribe(
                                          emptyAction.Action))
            };

            lightweightPubSubEvent.Subscribe(subscriptionAction.Action);

            Assert.False(lightweightPubSubEvent.Contains(emptyAction.Action));

            lightweightPubSubEvent.Publish();

            Assert.True((lightweightPubSubEvent.Contains(emptyAction.Action)));
        }

        [Fact]
        public void InlineDelegateDeclarationsDoesNotGetCollectedIncorrectlyWithWeakReferences()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();
            bool published = false;
            PubSubEvent.Subscribe(delegate { published = true; }, ThreadOption.PublisherThread, false, delegate { return true; });
            GC.Collect();
            PubSubEvent.Publish(null);

            Assert.True(published);
        }

        [Fact]
        public void InlineDelegateDeclarationsDoesNotGetCollectedIncorrectlyWithWeakReferencesNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();
            bool published = false;
            lightweightPubSubEvent.Subscribe(delegate { published = true; }, ThreadOption.PublisherThread, false);
            GC.Collect();
            lightweightPubSubEvent.Publish();

            Assert.True(published);
        }

        [Fact]
        public void ShouldNotGarbageCollectDelegateReferenceWhenUsingKeepAlive()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();

            var externalAction = new ExternalAction();
            PubSubEvent.Subscribe(externalAction.ExecuteAction, ThreadOption.PublisherThread, true);

            WeakReference actionEventReference = new WeakReference(externalAction);
            externalAction = null;
            GC.Collect();
            GC.Collect();
            Assert.True(actionEventReference.IsAlive);

            PubSubEvent.Publish("testPayload");

            Assert.Equal("testPayload", ((ExternalAction)actionEventReference.Target).PassedValue);
        }

        [Fact]
        public void ShouldNotGarbageCollectDelegateReferenceWhenUsingKeepAliveNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();

            var externalAction = new ExternalAction();
            lightweightPubSubEvent.Subscribe(externalAction.ExecuteAction, ThreadOption.PublisherThread, true);

            WeakReference actionEventReference = new WeakReference(externalAction);
            externalAction = null;
            GC.Collect();
            GC.Collect();
            Assert.True(actionEventReference.IsAlive);

            lightweightPubSubEvent.Publish();

            Assert.True(((ExternalAction)actionEventReference.Target).Executed);
        }

        [Fact]
        public void RegisterReturnsTokenThatCanBeUsedToUnsubscribe()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();
            var emptyAction = new ActionHelper();

            var token = PubSubEvent.Subscribe(emptyAction.Action);
            PubSubEvent.Unsubscribe(token);

            Assert.False(PubSubEvent.Contains(emptyAction.Action));
        }

        [Fact]
        public void RegisterReturnsTokenThatCanBeUsedToUnsubscribeNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();
            var emptyAction = new ActionHelper();

            var token = lightweightPubSubEvent.Subscribe(emptyAction.Action);
            lightweightPubSubEvent.Unsubscribe(token);

            Assert.False(lightweightPubSubEvent.Contains(emptyAction.Action));
        }

        [Fact]
        public void ContainsShouldSearchByToken()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();
            var emptyAction = new ActionHelper();
            var token = PubSubEvent.Subscribe(emptyAction.Action);

            Assert.True(PubSubEvent.Contains(token));

            PubSubEvent.Unsubscribe(emptyAction.Action);
            Assert.False(PubSubEvent.Contains(token));
        }

        [Fact]
        public void ContainsShouldSearchByTokenNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();
            var emptyAction = new ActionHelper();
            var token = lightweightPubSubEvent.Subscribe(emptyAction.Action);

            Assert.True(lightweightPubSubEvent.Contains(token));

            lightweightPubSubEvent.Unsubscribe(emptyAction.Action);
            Assert.False(lightweightPubSubEvent.Contains(token));
        }

        [Fact]
        public void SubscribeDefaultsToPublisherThread()
        {
            var PubSubEvent = new TestableLightweightPubSubEvent<string>();
            Action<string> action = delegate { };
            var token = PubSubEvent.Subscribe(action, true);

            Assert.Single(PubSubEvent.BaseSubscriptions);
            Assert.Equal(typeof(EventSubscription<string>), PubSubEvent.BaseSubscriptions.ElementAt(0).GetType());
        }

        [Fact]
        public void SubscribeDefaultsToPublisherThreadNonGeneric()
        {
            var lightweightPubSubEvent = new TestableLightweightPubSubEvent();
            Action action = delegate { };
            var token = lightweightPubSubEvent.Subscribe(action, true);

            Assert.Single(lightweightPubSubEvent.BaseSubscriptions);
            Assert.Equal(typeof(EventSubscription), lightweightPubSubEvent.BaseSubscriptions.ElementAt(0).GetType());
        }

        public class ExternalFilter
        {
            public bool AlwaysTrueFilter(string value)
            {
                return true;
            }
        }

        public class ExternalAction
        {
            public string PassedValue;
            public bool Executed = false;

            public void ExecuteAction(string value)
            {
                PassedValue = value;
                Executed = true;
            }

            public void ExecuteAction()
            {
                Executed = true;
            }
        }

        class TestableLightweightPubSubEvent<TPayload> : LightweightPubSubEvent<TPayload>
        {
            public ICollection<IEventSubscription> BaseSubscriptions
            {
                get { return base.Subscriptions; }
            }
        }

        class TestableLightweightPubSubEvent : LightweightPubSubEvent
        {
            public ICollection<IEventSubscription> BaseSubscriptions
            {
                get { return base.Subscriptions; }
            }
        }

        public class Payload { }
    }

    public class ActionHelper
    {
        public bool ActionCalled;
        public Action ActionToExecute = null;
        private object actionArg;

        public T ActionArg<T>()
        {
            return (T)actionArg;
        }

        public void Action(LightweightPubSubEventFixture.Payload arg)
        {
            Action((object)arg);
        }

        public void Action(string arg)
        {
            Action((object)arg);
        }

        public void Action(object arg)
        {
            actionArg = arg;
            ActionCalled = true;
            if (ActionToExecute != null)
            {
                ActionToExecute.Invoke();
            }
        }

        public void Action()
        {
            ActionCalled = true;
            if (ActionToExecute != null)
            {
                ActionToExecute.Invoke();
            }
        }
    }

    public class MockFilter
    {
        public bool FilterReturnValue;

        public bool FilterString(string arg)
        {
            return FilterReturnValue;
        }
    }
}
