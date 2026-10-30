using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Prism.Events.Extensions.Tests;

/// <summary>
/// Exposes the protected members of <see cref="LightweightPubSubEvent"/> to tests.
/// </summary>
internal sealed class InspectableLightweightPubSubEvent : LightweightPubSubEvent
{
    public int SubscriptionCount
    {
        get { lock (Subscriptions) return Subscriptions.Count; }
    }

    public void PublishThroughEventBase(params object[] arguments) => InternalPublish(arguments);
}

/// <summary>
/// Exposes the protected members of <see cref="LightweightPubSubEvent{TPayload}"/> to tests.
/// </summary>
internal sealed class InspectableLightweightPubSubEvent<TPayload> : LightweightPubSubEvent<TPayload>
{
    public int SubscriptionCount
    {
        get { lock (Subscriptions) return Subscriptions.Count; }
    }
}

/// <summary>
/// Exposes the protected members of <see cref="AsyncPubSubEvent"/> to tests.
/// </summary>
internal sealed class InspectableAsyncPubSubEvent : AsyncPubSubEvent
{
    public int SubscriptionCount
    {
        get { lock (Subscriptions) return Subscriptions.Count; }
    }
}

/// <summary>
/// Exposes the protected members of <see cref="AsyncPubSubEvent{TPayload}"/> to tests.
/// </summary>
internal sealed class InspectableAsyncPubSubEvent<TPayload> : AsyncPubSubEvent<TPayload>
{
    public int SubscriptionCount
    {
        get { lock (Subscriptions) return Subscriptions.Count; }
    }
}

/// <summary>
/// A subscriber object whose instance methods are used as weakly referenced callbacks.
/// </summary>
internal sealed class Subscriber
{
    private int invocationCount;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public void Handle() => Interlocked.Increment(ref invocationCount);

    public void Handle(int payload) => Interlocked.Increment(ref invocationCount);

    public ValueTask HandleAsync()
    {
        Interlocked.Increment(ref invocationCount);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(int payload)
    {
        Interlocked.Increment(ref invocationCount);
        return ValueTask.CompletedTask;
    }

    public bool Accept(int payload) => true;
}

/// <summary>
/// A <see cref="SynchronizationContext"/> that runs every posted callback on one dedicated thread,
/// like a UI dispatcher.
/// </summary>
internal sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object State)> queue = new();
    private readonly Thread thread;

    public SingleThreadSynchronizationContext()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "Test UI thread" };
        thread.Start();
    }

    public int ManagedThreadId => thread.ManagedThreadId;

    public override void Post(SendOrPostCallback d, object state) => queue.Add((d, state));

    public override SynchronizationContext CreateCopy() => this;

    public void Dispose()
    {
        queue.CompleteAdding();
        thread.Join(TimeSpan.FromSeconds(10));
        queue.Dispose();
    }

    private void Run()
    {
        SetSynchronizationContext(this);
        foreach (var (callback, state) in queue.GetConsumingEnumerable())
        {
            callback(state);
        }
    }
}

/// <summary>
/// A <see cref="SynchronizationContext"/> that records posted callbacks so that a test can run them
/// later, or inspect exceptions raised by <see langword="async"/> <see langword="void"/> methods.
/// </summary>
internal sealed class RecordingSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> posted = new();

    public int PostedCount => posted.Count;

    public override void Post(SendOrPostCallback d, object state) => posted.Enqueue((d, state));

    public override void OperationStarted()
    {
    }

    public override void OperationCompleted()
    {
    }

    /// <summary>Runs every queued callback and returns the exceptions they threw.</summary>
    public List<Exception> RunAll()
    {
        var exceptions = new List<Exception>();
        while (posted.TryDequeue(out var item))
        {
            try
            {
                item.Callback(item.State);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        return exceptions;
    }
}

internal static class GarbageCollection
{
    /// <summary>
    /// Runs full blocking collections until the finalizer queue is drained.
    /// </summary>
    public static void Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>
    /// Creates a subscriber, passes it to <paramref name="subscribe"/>, and returns only a weak
    /// reference to it, so that no stack slot in the caller keeps it alive.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WeakReference CreateSubscriber(Action<Subscriber> subscribe)
    {
        var subscriber = new Subscriber();
        subscribe(subscriber);
        return new WeakReference(subscriber);
    }
}
