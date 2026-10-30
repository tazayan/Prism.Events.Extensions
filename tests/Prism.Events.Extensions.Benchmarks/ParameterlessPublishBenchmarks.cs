using BenchmarkDotNet.Attributes;

namespace Prism.Events.Extensions.Benchmarks;

/// <summary>
/// Measures publishing an event without a payload to publisher-thread subscribers.
/// </summary>
/// <remarks>The baseline is Prism's <see cref="PubSubEvent"/>.</remarks>
public class ParameterlessPublishBenchmarks
{
    private PubSubEvent prismEvent = null!;
    private LightweightPubSubEvent lightweightEvent = null!;
    private AsyncPubSubEvent asyncEvent = null!;
    private Subscriber[] subscribers = null!;

    [Params(1, 10, 100)]
    public int SubscriberCount { get; set; }

    [Params(true, false)]
    public bool KeepSubscriberReferenceAlive { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        prismEvent = new PubSubEvent();
        lightweightEvent = new LightweightPubSubEvent();
        asyncEvent = new AsyncPubSubEvent();
        subscribers = Subscriber.CreateMany(SubscriberCount);

        foreach (var subscriber in subscribers)
        {
            prismEvent.Subscribe(subscriber.Handle, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive);
            lightweightEvent.Subscribe(subscriber.Handle, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive);
            asyncEvent.Subscribe(subscriber.HandleAsync, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive);
        }
    }

    [Benchmark(Baseline = true, Description = "Prism PubSubEvent.Publish")]
    public void PrismPublish() => prismEvent.Publish();

    [Benchmark(Description = "LightweightPubSubEvent.Publish")]
    public void LightweightPublish() => lightweightEvent.Publish();

    [Benchmark(Description = "AsyncPubSubEvent.Send")]
    public ValueTask AsyncSend() => asyncEvent.Send();
}
