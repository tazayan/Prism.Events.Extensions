using BenchmarkDotNet.Attributes;

namespace Prism.Events.Extensions.Benchmarks;

/// <summary>
/// Measures publishing to subscribers that have a filter, alternating between payloads the filter
/// accepts and payloads it rejects.
/// </summary>
/// <remarks>The baseline is Prism's <see cref="PubSubEvent{TPayload}"/>.</remarks>
public class FilteredPublishBenchmarks
{
    private const int PublicationsPerInvocation = 2;

    private PubSubEvent<int> prismEvent = null!;
    private LightweightPubSubEvent<int> lightweightEvent = null!;
    private AsyncPubSubEvent<int> asyncEvent = null!;
    private Subscriber[] subscribers = null!;

    [Params(10, 100)]
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
            prismEvent.Subscribe(subscriber.Handle, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive, subscriber.AcceptEven);
            lightweightEvent.Subscribe(subscriber.Handle, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive, subscriber.AcceptEven);
            asyncEvent.Subscribe(subscriber.HandleAsync, ThreadOption.PublisherThread, KeepSubscriberReferenceAlive, subscriber.AcceptEven);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = PublicationsPerInvocation, Description = "Prism PubSubEvent<int>.Publish")]
    public void PrismPublish()
    {
        prismEvent.Publish(1);
        prismEvent.Publish(2);
    }

    [Benchmark(OperationsPerInvoke = PublicationsPerInvocation, Description = "LightweightPubSubEvent<int>.Publish")]
    public void LightweightPublish()
    {
        lightweightEvent.Publish(1);
        lightweightEvent.Publish(2);
    }

    [Benchmark(OperationsPerInvoke = PublicationsPerInvocation, Description = "AsyncPubSubEvent<int>.Send")]
    public async ValueTask AsyncSend()
    {
        await asyncEvent.Send(1);
        await asyncEvent.Send(2);
    }
}
