using BenchmarkDotNet.Attributes;

namespace Prism.Events.Extensions.Benchmarks;

/// <summary>
/// Measures publishing an <see cref="int"/> payload to publisher-thread subscribers.
/// </summary>
/// <remarks>
/// The baseline is Prism's <see cref="PubSubEvent{TPayload}"/>. A value-type payload shows the cost of
/// boxing in Prism's object-array publish path. Weakly referenced subscriptions, the default in Prism,
/// resolve a new delegate from their weak reference on every publish, so they are measured separately.
/// </remarks>
public class PublishBenchmarks
{
    private PubSubEvent<int> prismEvent = null!;
    private LightweightPubSubEvent<int> lightweightEvent = null!;
    private AsyncPubSubEvent<int> asyncEvent = null!;
    private Subscriber[] subscribers = null!;

    [Params(0, 1, 10, 100)]
    public int SubscriberCount { get; set; }

    [Params(true, false)]
    public bool KeepSubscriberReferenceAlive { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        prismEvent = new PubSubEvent<int>();
        lightweightEvent = new LightweightPubSubEvent<int>();
        asyncEvent = new AsyncPubSubEvent<int>();
        subscribers = Subscriber.CreateMany(SubscriberCount);

        foreach (var subscriber in subscribers)
        {
            prismEvent.Subscribe(subscriber.Handle, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive);
            lightweightEvent.Subscribe(subscriber.Handle, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive);
            asyncEvent.Subscribe(subscriber.HandleAsync, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive);
        }
    }

    [Benchmark(Baseline = true, Description = "Prism PubSubEvent<int>.Publish")]
    public void PrismPublish() => prismEvent.Publish(1);

    [Benchmark(Description = "LightweightPubSubEvent<int>.Publish")]
    public void LightweightPublish() => lightweightEvent.Publish(1);

    [Benchmark(Description = "AsyncPubSubEvent<int>.Send")]
    public ValueTask AsyncSend() => asyncEvent.Send(1);

    [Benchmark(Description = "AsyncPubSubEvent<int>.Publish")]
    public void AsyncPublish() => asyncEvent.Publish(1);
}
