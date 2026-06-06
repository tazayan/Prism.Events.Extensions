using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace Prism.Events.Extensions.Benchmarks;
[CPUUsageDiagnoser]
[MemoryDiagnoser]
public class PublishBenchmarks
{
    private static readonly Action<int> Handler = static _ =>
    {
    };
    private PubSubEvent<int> _prismEvent = null!;
    private LightweightPubSubEvent<int> _lightweightEvent = null!;
    [Params(1, 10, 100)]
    public int SubscriberCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _prismEvent = new PubSubEvent<int>();
        _lightweightEvent = new LightweightPubSubEvent<int>();
        for (var i = 0; i < SubscriberCount; i++)
        {
            _prismEvent.Subscribe(Handler, ThreadOption.PublisherThread, true);
            _lightweightEvent.Subscribe(Handler, ThreadOption.PublisherThread, true);
        }
    }

    [Benchmark(Baseline = true)]
    public void PrismPubSubEventPublish()
    {
        _prismEvent.Publish(42);
    }

    [Benchmark]
    public void LightweightPubSubEventPublish()
    {
        _lightweightEvent.Publish(42);
    }
}
