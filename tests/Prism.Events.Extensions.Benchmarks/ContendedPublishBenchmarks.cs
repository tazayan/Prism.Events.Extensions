using BenchmarkDotNet.Attributes;

namespace Prism.Events.Extensions.Benchmarks;

/// <summary>
/// Measures publishing from several threads at once to the same event, which contends for the
/// event's subscription lock.
/// </summary>
/// <remarks>
/// Each invocation starts <see cref="ThreadCount"/> workers on dedicated threads, and each worker
/// publishes <see cref="PublicationsPerThread"/> times. The reported time is per publication. The
/// baseline is Prism's <see cref="PubSubEvent{TPayload}"/>.
/// </remarks>
public class ContendedPublishBenchmarks
{
    private const int PublicationsPerThread = 10_000;
    private const int ThreadCount = 4;

    private PubSubEvent<int> prismEvent = null!;
    private LightweightPubSubEvent<int> lightweightEvent = null!;
    private AsyncPubSubEvent<int> asyncEvent = null!;
    private Subscriber[] subscribers = null!;

    [Params(10)]
    public int SubscriberCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        prismEvent = new PubSubEvent<int>();
        lightweightEvent = new LightweightPubSubEvent<int>();
        asyncEvent = new AsyncPubSubEvent<int>();
        subscribers = Subscriber.CreateMany(SubscriberCount);

        foreach (var subscriber in subscribers)
        {
            prismEvent.Subscribe(subscriber.Handle, keepSubscriberReferenceAlive: true);
            lightweightEvent.Subscribe(subscriber.Handle, keepSubscriberReferenceAlive: true);
            asyncEvent.Subscribe(subscriber.HandleAsync, keepSubscriberReferenceAlive: true);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = PublicationsPerThread * ThreadCount, Description = "Prism PubSubEvent<int>.Publish")]
    public void PrismPublish() => RunOnThreads(() =>
    {
        for (var i = 0; i < PublicationsPerThread; i++)
        {
            prismEvent.Publish(1);
        }
    });

    [Benchmark(OperationsPerInvoke = PublicationsPerThread * ThreadCount, Description = "LightweightPubSubEvent<int>.Publish")]
    public void LightweightPublish() => RunOnThreads(() =>
    {
        for (var i = 0; i < PublicationsPerThread; i++)
        {
            lightweightEvent.Publish(1);
        }
    });

    [Benchmark(OperationsPerInvoke = PublicationsPerThread * ThreadCount, Description = "AsyncPubSubEvent<int>.Send")]
    public void AsyncSend() => RunOnThreads(() =>
    {
        for (var i = 0; i < PublicationsPerThread; i++)
        {
            // Every subscriber completes synchronously, so the ValueTask is already complete.
            asyncEvent.Send(1).GetAwaiter().GetResult();
        }
    });

    private static void RunOnThreads(Action work)
    {
        var threads = new Thread[ThreadCount];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() => work());
            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }
    }
}
