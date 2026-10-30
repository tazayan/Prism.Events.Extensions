using System.Collections.Concurrent;
using Xunit;

namespace Prism.Events.Extensions.Tests;

/// <summary>
/// Verifies thread safety and snapshot semantics when events are published, subscribed to and
/// unsubscribed from concurrently, including from inside subscriber callbacks.
/// </summary>
public class ConcurrencyFixture
{
    private const int PublisherCount = 8;
    private const int PublicationsPerPublisher = 2_000;
    private const int SubscriberCount = 16;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ConcurrentLightweightPublishersDeliverEveryPublicationToEverySubscriber()
    {
        var pubSubEvent = new LightweightPubSubEvent<int>();
        var invocationCount = 0;
        long payloadSum = 0;
        for (var i = 0; i < SubscriberCount; i++)
        {
            pubSubEvent.Subscribe(payload =>
            {
                Interlocked.Increment(ref invocationCount);
                Interlocked.Add(ref payloadSum, payload);
            }, keepSubscriberReferenceAlive: true);
        }

        await RunConcurrently(PublisherCount, publisher =>
        {
            for (var i = 0; i < PublicationsPerPublisher; i++)
            {
                pubSubEvent.Publish(1);
            }
        });

        Assert.Equal(PublisherCount * PublicationsPerPublisher * SubscriberCount, invocationCount);
        Assert.Equal(PublisherCount * PublicationsPerPublisher * SubscriberCount, payloadSum);
    }

    [Fact]
    public async Task ConcurrentParameterlessLightweightPublishersDeliverEveryPublication()
    {
        var pubSubEvent = new LightweightPubSubEvent();
        var invocationCount = 0;
        for (var i = 0; i < SubscriberCount; i++)
        {
            pubSubEvent.Subscribe(() => Interlocked.Increment(ref invocationCount), keepSubscriberReferenceAlive: true);
        }

        await RunConcurrently(PublisherCount, _ =>
        {
            for (var i = 0; i < PublicationsPerPublisher; i++)
            {
                pubSubEvent.Publish();
            }
        });

        Assert.Equal(PublisherCount * PublicationsPerPublisher * SubscriberCount, invocationCount);
    }

    [Fact]
    public async Task ConcurrentSendsCompleteAndDeliverEveryPublicationToEverySubscriber()
    {
        var parameterlessEvent = new AsyncPubSubEvent();
        var payloadEvent = new AsyncPubSubEvent<int>();
        var parameterlessCount = 0;
        var payloadCount = 0;
        for (var i = 0; i < SubscriberCount; i++)
        {
            parameterlessEvent.Subscribe(async () =>
            {
                await Task.Yield();
                Interlocked.Increment(ref parameterlessCount);
            }, keepSubscriberReferenceAlive: true);
            payloadEvent.Subscribe(async payload =>
            {
                await Task.Yield();
                Interlocked.Add(ref payloadCount, payload);
            }, keepSubscriberReferenceAlive: true);
        }

        const int sendsPerPublisher = 200;
        await RunConcurrentlyAsync(PublisherCount, async _ =>
        {
            for (var i = 0; i < sendsPerPublisher; i++)
            {
                await parameterlessEvent.Send();
                await payloadEvent.Send(1);
            }
        });

        Assert.Equal(PublisherCount * sendsPerPublisher * SubscriberCount, parameterlessCount);
        Assert.Equal(PublisherCount * sendsPerPublisher * SubscriberCount, payloadCount);
    }

    [Fact]
    public async Task SendRunsTheSubscribersOfOnePublicationOneAtATimeEvenWhenTheyRunOnBackgroundThreads()
    {
        var pubSubEvent = new AsyncPubSubEvent<int>();
        var active = 0;
        var maximumActive = 0;
        var invocationCount = 0;
        for (var i = 0; i < SubscriberCount; i++)
        {
            pubSubEvent.Subscribe(async _ =>
            {
                var nowActive = Interlocked.Increment(ref active);
                InterlockedMax(ref maximumActive, nowActive);
                await Task.Delay(1);
                Interlocked.Increment(ref invocationCount);
                Interlocked.Decrement(ref active);
            }, ThreadOption.BackgroundThread, keepSubscriberReferenceAlive: true);
        }

        await pubSubEvent.Send(1).AsTask().WaitAsync(TestTimeout);

        Assert.Equal(1, maximumActive);
        Assert.Equal(SubscriberCount, invocationCount);
    }

    [Fact]
    public async Task LightweightEventToleratesSubscribeAndUnsubscribeWhilePublishing()
    {
        var pubSubEvent = new InspectableLightweightPubSubEvent<int>();
        var stableInvocations = 0;
        pubSubEvent.Subscribe(_ => Interlocked.Increment(ref stableInvocations), keepSubscriberReferenceAlive: true);

        using var stop = new CancellationTokenSource();
        var publishCount = 0;
        var publishers = RunConcurrently(4, _ =>
        {
            while (!stop.IsCancellationRequested)
            {
                pubSubEvent.Publish(1);
                Interlocked.Increment(ref publishCount);
            }
        });

        await RunConcurrently(4, _ =>
        {
            for (var i = 0; i < 2_000; i++)
            {
                var marker = new object();
                Action<int> handler = _ => GC.KeepAlive(marker);
                var token = pubSubEvent.Subscribe(handler, keepSubscriberReferenceAlive: true);
                Assert.True(pubSubEvent.Contains(handler));
                if (i % 2 == 0)
                {
                    token.Dispose();
                }
                else
                {
                    pubSubEvent.Unsubscribe(handler);
                }

                Assert.False(pubSubEvent.Contains(handler));
            }
        });

        stop.Cancel();
        await publishers;

        Assert.Equal(1, pubSubEvent.SubscriptionCount);
        Assert.Equal(publishCount, stableInvocations);
    }

    [Fact]
    public async Task AsyncEventToleratesSubscribeAndUnsubscribeWhileSending()
    {
        var pubSubEvent = new InspectableAsyncPubSubEvent();
        var stableInvocations = 0;
        pubSubEvent.Subscribe(() =>
        {
            Interlocked.Increment(ref stableInvocations);
            return ValueTask.CompletedTask;
        }, keepSubscriberReferenceAlive: true);

        using var stop = new CancellationTokenSource();
        var sendCount = 0;
        var publishers = RunConcurrentlyAsync(4, async _ =>
        {
            while (!stop.IsCancellationRequested)
            {
                await pubSubEvent.Send();
                Interlocked.Increment(ref sendCount);
            }
        });

        await RunConcurrently(4, _ =>
        {
            for (var i = 0; i < 2_000; i++)
            {
                var marker = new object();
                Func<ValueTask> handler = () =>
                {
                    GC.KeepAlive(marker);
                    return ValueTask.CompletedTask;
                };
                var token = pubSubEvent.Subscribe(handler, keepSubscriberReferenceAlive: true);
                Assert.True(pubSubEvent.Contains(handler));
                if (i % 2 == 0)
                {
                    token.Dispose();
                }
                else
                {
                    pubSubEvent.Unsubscribe(handler);
                }

                Assert.False(pubSubEvent.Contains(handler));
            }
        });

        stop.Cancel();
        await publishers;

        Assert.Equal(1, pubSubEvent.SubscriptionCount);
        Assert.Equal(sendCount, stableInvocations);
    }

    [Fact]
    public async Task PublishingWhileTheGarbageCollectorReclaimsWeakSubscribersPrunesThemSafely()
    {
        var lightweightEvent = new InspectableLightweightPubSubEvent<int>();
        var asyncEvent = new InspectableAsyncPubSubEvent<int>();
        var strongSubscriber = new Subscriber();
        lightweightEvent.Subscribe(strongSubscriber.Handle, keepSubscriberReferenceAlive: true);
        asyncEvent.Subscribe(strongSubscriber.HandleAsync, keepSubscriberReferenceAlive: true);

        using var stop = new CancellationTokenSource();
        var publishers = RunConcurrentlyAsync(4, async _ =>
        {
            while (!stop.IsCancellationRequested)
            {
                lightweightEvent.Publish(1);
                await asyncEvent.Send(1);
            }
        });

        await RunConcurrently(2, _ =>
        {
            for (var i = 0; i < 500; i++)
            {
                GarbageCollection.CreateSubscriber(s =>
                {
                    lightweightEvent.Subscribe(s.Handle);
                    asyncEvent.Subscribe(s.HandleAsync);
                });

                if (i % 50 == 0)
                {
                    GC.Collect();
                }
            }
        });

        stop.Cancel();
        await publishers;

        GarbageCollection.Collect();
        lightweightEvent.Publish(1);
        await asyncEvent.Send(1);

        Assert.Equal(1, lightweightEvent.SubscriptionCount);
        Assert.Equal(1, asyncEvent.SubscriptionCount);
    }

    [Fact]
    public void LightweightPublicationUsesTheSubscriptionsThatExistedWhenItStarted()
    {
        var pubSubEvent = new LightweightPubSubEvent<int>();
        var calls = new List<string>();
        Action<int> second = _ => calls.Add("second");
        Action<int> added = _ => calls.Add("added");
        pubSubEvent.Subscribe(_ =>
        {
            calls.Add("first");
            pubSubEvent.Unsubscribe(second);
            pubSubEvent.Subscribe(added, keepSubscriberReferenceAlive: true);
        }, keepSubscriberReferenceAlive: true);
        pubSubEvent.Subscribe(second, keepSubscriberReferenceAlive: true);

        pubSubEvent.Publish(1);
        Assert.Equal(new[] { "first", "second" }, calls);

        calls.Clear();
        pubSubEvent.Publish(2);
        Assert.Equal(new[] { "first", "added" }, calls);
    }

    [Fact]
    public async Task AsyncPublicationUsesTheSubscriptionsThatExistedWhenItStarted()
    {
        var pubSubEvent = new AsyncPubSubEvent<int>();
        var calls = new List<string>();
        Func<int, ValueTask> second = _ =>
        {
            calls.Add("second");
            return ValueTask.CompletedTask;
        };
        Func<int, ValueTask> added = _ =>
        {
            calls.Add("added");
            return ValueTask.CompletedTask;
        };
        pubSubEvent.Subscribe(async _ =>
        {
            calls.Add("first");
            await Task.Yield();
            pubSubEvent.Unsubscribe(second);
            pubSubEvent.Subscribe(added, keepSubscriberReferenceAlive: true);
        }, keepSubscriberReferenceAlive: true);
        pubSubEvent.Subscribe(second, keepSubscriberReferenceAlive: true);

        await pubSubEvent.Send(1);
        Assert.Equal(new[] { "first", "second" }, calls);

        calls.Clear();
        await pubSubEvent.Send(2);
        Assert.Equal(new[] { "first", "added" }, calls);
    }

    [Fact]
    public async Task SubscribersCanPublishReentrantlyAndUnsubscribeThemselves()
    {
        var lightweightEvent = new InspectableLightweightPubSubEvent<int>();
        var asyncEvent = new InspectableAsyncPubSubEvent<int>();
        var lightweightPayloads = new List<int>();
        var asyncPayloads = new List<int>();
        SubscriptionToken lightweightToken = null;
        SubscriptionToken asyncToken = null;

        lightweightToken = lightweightEvent.Subscribe(payload =>
        {
            lightweightPayloads.Add(payload);
            if (payload > 0)
            {
                lightweightEvent.Publish(payload - 1);
            }
            else
            {
                lightweightToken.Dispose();
            }
        }, keepSubscriberReferenceAlive: true);
        asyncToken = asyncEvent.Subscribe(async payload =>
        {
            asyncPayloads.Add(payload);
            if (payload > 0)
            {
                await asyncEvent.Send(payload - 1);
            }
            else
            {
                asyncToken.Dispose();
            }
        }, keepSubscriberReferenceAlive: true);

        lightweightEvent.Publish(3);
        await asyncEvent.Send(3).AsTask().WaitAsync(TestTimeout);

        Assert.Equal(new[] { 3, 2, 1, 0 }, lightweightPayloads);
        Assert.Equal(new[] { 3, 2, 1, 0 }, asyncPayloads);
        Assert.Equal(0, lightweightEvent.SubscriptionCount);
        Assert.Equal(0, asyncEvent.SubscriptionCount);
    }

    [Fact]
    public void BackgroundSubscriptionsRunOffThePublisherThread()
    {
        var publisherThread = 0;
        Exception publisherException = null;
        var threads = new ConcurrentBag<int>();
        using var countdown = new CountdownEvent(2);
        var lightweightEvent = new LightweightPubSubEvent();
        var lightweightPayloadEvent = new LightweightPubSubEvent<int>();
        var asyncEvent = new AsyncPubSubEvent();
        var asyncPayloadEvent = new AsyncPubSubEvent<int>();
        lightweightEvent.Subscribe(() => { threads.Add(Environment.CurrentManagedThreadId); countdown.Signal(); }, ThreadOption.BackgroundThread, true);
        lightweightPayloadEvent.Subscribe(_ => { threads.Add(Environment.CurrentManagedThreadId); countdown.Signal(); }, ThreadOption.BackgroundThread, true);
        asyncEvent.Subscribe(() => { threads.Add(Environment.CurrentManagedThreadId); return ValueTask.CompletedTask; }, ThreadOption.BackgroundThread, true);
        asyncPayloadEvent.Subscribe(_ => { threads.Add(Environment.CurrentManagedThreadId); return ValueTask.CompletedTask; }, ThreadOption.BackgroundThread, true);

        var publisher = new Thread(() =>
        {
            publisherThread = Environment.CurrentManagedThreadId;
            try
            {
                lightweightEvent.Publish();
                lightweightPayloadEvent.Publish(1);
                asyncEvent.Send().AsTask().GetAwaiter().GetResult();
                asyncPayloadEvent.Send(1).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                publisherException = exception;
            }
        });

        publisher.Start();

        Assert.True(publisher.Join(TestTimeout));
        Assert.Null(publisherException);
        Assert.True(countdown.Wait(TestTimeout));
        Assert.Equal(4, threads.Count);
        Assert.DoesNotContain(publisherThread, threads);
    }

    [Fact]
    public async Task UIThreadSubscriptionsRunOnTheSynchronizationContextThread()
    {
        using var uiContext = new SingleThreadSynchronizationContext();
        var threads = new ConcurrentBag<int>();
        using var countdown = new CountdownEvent(2);
        var lightweightEvent = new LightweightPubSubEvent { SynchronizationContext = uiContext };
        var lightweightPayloadEvent = new LightweightPubSubEvent<int> { SynchronizationContext = uiContext };
        var asyncEvent = new AsyncPubSubEvent { SynchronizationContext = uiContext };
        var asyncPayloadEvent = new AsyncPubSubEvent<int> { SynchronizationContext = uiContext };
        var asyncContinuationThreads = new ConcurrentBag<int>();

        lightweightEvent.Subscribe(() => { threads.Add(Environment.CurrentManagedThreadId); countdown.Signal(); }, ThreadOption.UIThread, true);
        lightweightPayloadEvent.Subscribe(_ => { threads.Add(Environment.CurrentManagedThreadId); countdown.Signal(); }, ThreadOption.UIThread, true);
        asyncEvent.Subscribe(async () =>
        {
            threads.Add(Environment.CurrentManagedThreadId);
            await Task.Delay(1);
            asyncContinuationThreads.Add(Environment.CurrentManagedThreadId);
        }, ThreadOption.UIThread, true);
        asyncPayloadEvent.Subscribe(async _ =>
        {
            threads.Add(Environment.CurrentManagedThreadId);
            await Task.Delay(1);
            asyncContinuationThreads.Add(Environment.CurrentManagedThreadId);
        }, ThreadOption.UIThread, true);

        lightweightEvent.Publish();
        lightweightPayloadEvent.Publish(1);
        await asyncEvent.Send().AsTask().WaitAsync(TestTimeout);
        await asyncPayloadEvent.Send(1).AsTask().WaitAsync(TestTimeout);

        Assert.True(countdown.Wait(TestTimeout));
        Assert.Equal(4, threads.Count);
        Assert.All(threads, thread => Assert.Equal(uiContext.ManagedThreadId, thread));
        Assert.Equal(2, asyncContinuationThreads.Count);
        Assert.All(asyncContinuationThreads, thread => Assert.Equal(uiContext.ManagedThreadId, thread));
    }

    private static async Task RunConcurrently(int workerCount, Action<int> work)
    {
        using var start = new Barrier(workerCount);
        var workers = new Task[workerCount];
        for (var i = 0; i < workerCount; i++)
        {
            var worker = i;
            workers[i] = Task.Factory.StartNew(() =>
            {
                start.SignalAndWait();
                work(worker);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        await Task.WhenAll(workers).WaitAsync(TestTimeout);
    }

    private static Task RunConcurrentlyAsync(int workerCount, Func<int, Task> work)
    {
        var workers = new Task[workerCount];
        for (var i = 0; i < workerCount; i++)
        {
            var worker = i;
            workers[i] = Task.Run(() => work(worker));
        }

        return Task.WhenAll(workers).WaitAsync(TestTimeout);
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int current;
        while ((current = Volatile.Read(ref location)) < value)
        {
            if (Interlocked.CompareExchange(ref location, value, current) == current)
            {
                return;
            }
        }
    }
}
