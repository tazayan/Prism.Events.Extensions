using BenchmarkDotNet.Attributes;

namespace Prism.Events.Extensions.Benchmarks;

/// <summary>
/// Measures the cost of subscribing a batch of callbacks and then unsubscribing all of them with
/// their tokens.
/// </summary>
/// <remarks>The baseline is Prism's <see cref="PubSubEvent{TPayload}"/>.</remarks>
public class SubscriptionBenchmarks
{
    private PubSubEvent<int> prismEvent = null!;
    private LightweightPubSubEvent<int> lightweightEvent = null!;
    private AsyncPubSubEvent<int> asyncEvent = null!;
    private Subscriber[] subscribers = null!;
    private SubscriptionToken[] tokens = null!;

    [Params(10, 100)]
    public int SubscriberCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        prismEvent = new PubSubEvent<int>();
        lightweightEvent = new LightweightPubSubEvent<int>();
        asyncEvent = new AsyncPubSubEvent<int>();
        subscribers = Subscriber.CreateMany(SubscriberCount);
        tokens = new SubscriptionToken[SubscriberCount];
    }

    [Benchmark(Baseline = true, Description = "Prism PubSubEvent<int>")]
    public void PrismSubscribeAndUnsubscribe()
    {
        for (var i = 0; i < subscribers.Length; i++)
        {
            tokens[i] = prismEvent.Subscribe(subscribers[i].Handle);
        }

        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            tokens[i].Dispose();
        }
    }

    [Benchmark(Description = "LightweightPubSubEvent<int>")]
    public void LightweightSubscribeAndUnsubscribe()
    {
        for (var i = 0; i < subscribers.Length; i++)
        {
            tokens[i] = lightweightEvent.Subscribe(subscribers[i].Handle);
        }

        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            tokens[i].Dispose();
        }
    }

    [Benchmark(Description = "AsyncPubSubEvent<int>")]
    public void AsyncSubscribeAndUnsubscribe()
    {
        for (var i = 0; i < subscribers.Length; i++)
        {
            tokens[i] = asyncEvent.Subscribe(subscribers[i].HandleAsync);
        }

        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            tokens[i].Dispose();
        }
    }
}
