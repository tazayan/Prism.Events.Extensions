# Benchmarks

These [BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks compare each event type in this library with the Prism type it replaces. Prism is always the baseline, so the `Ratio` column reads as "faster/slower than Prism".

| Class | What it measures | Parameters |
|---|---|---|
| `PublishBenchmarks` | Publishing an `int` payload to publisher-thread subscribers: `PubSubEvent<int>.Publish` vs `LightweightPubSubEvent<int>.Publish`, `AsyncPubSubEvent<int>.Send` and `AsyncPubSubEvent<int>.Publish` | 0, 1, 10 or 100 subscribers; strong or weak references |
| `ParameterlessPublishBenchmarks` | The same for events without a payload | 1, 10 or 100 subscribers; strong or weak references |
| `FilteredPublishBenchmarks` | Subscribers with a filter, alternating accepted and rejected payloads | 10 or 100 subscribers; strong or weak references |
| `SubscriptionBenchmarks` | Subscribing a batch of callbacks and disposing their tokens | 10 or 100 subscribers |
| `ContendedPublishBenchmarks` | Four threads publishing to the same event at once | 10 subscribers |

Every benchmark reports allocated bytes (`MemoryDiagnoser`), plus lock contentions and thread-pool work items (`ThreadingDiagnoser`).

## Running

Run the benchmarks in Release, from the repository root:

```powershell
# Everything (takes a while)
dotnet run -c Release --project tests/Prism.Events.Extensions.Benchmarks -- --filter *

# One class
dotnet run -c Release --project tests/Prism.Events.Extensions.Benchmarks -- --filter *PublishBenchmarks*

# Quick, less precise pass
dotnet run -c Release --project tests/Prism.Events.Extensions.Benchmarks -- --filter * --job short

# Both target frameworks side by side
dotnet run -c Release -f net9.0 --project tests/Prism.Events.Extensions.Benchmarks -- --filter * --runtimes net8.0 net9.0
```

On Windows, add `--cpu-usage` to also collect a Visual Studio CPU usage trace for each benchmark. Results are written to `BenchmarkDotNet.Artifacts/results`.

For stable numbers, close other applications, use a power plan that does not throttle the CPU, and don't compare runs from different machines.

## Indicative results

These numbers come from a single `--job short` run on a shared 2-vCPU Linux VM (.NET 8), so they are noisy. Use them as a rough guide to the shape of the results, not as reference figures; measure on your own hardware before you publish numbers.

`PublishBenchmarks`, mean time per publish and bytes allocated per publish:

| Subscribers | References | Prism `PubSubEvent<int>` | `LightweightPubSubEvent<int>` | `AsyncPubSubEvent<int>.Send` |
|---:|---|---:|---:|---:|
| 0 | either | 44 ns, 88 B | 4 ns, 0 B | 25 ns, 0 B |
| 10 | strong | 650 ns, 1,424 B | 153 ns, 0 B | 256 ns, 0 B |
| 100 | strong | 4,992 ns, 12,648 B | 921 ns, 0 B | 2,044 ns, 0 B |
| 10 | weak | 3,660 ns, 2,064 B | 2,958 ns, 640 B | 4,006 ns, 640 B |
| 100 | weak | 34,240 ns, 19,048 B | 28,172 ns, 6,400 B | 43,597 ns, 6,400 B |

What to expect:

- **Strongly referenced subscribers** (`keepSubscriberReferenceAlive: true`): the lightweight event is roughly 4–5× faster than Prism with 10 or more subscribers, and neither replacement allocates.
- **Weakly referenced subscribers**, Prism's default, are dominated by Prism's `DelegateReference`. It calls `MethodInfo.CreateDelegate` every time a weak delegate is resolved, which costs about 300 ns and 64 bytes each time. Both libraries resolve each delegate once per publish, so the gain there is smaller: about 3× less memory and a modest speed-up for the lightweight event. `AsyncPubSubEvent` performs about the same as Prism.
- **Contention**: with four threads publishing at once, the lightweight event was about 3.7× faster than Prism, because it holds the subscription lock only long enough to copy one array.
- **Subscribing and unsubscribing** costs about the same as Prism, because it uses the same `DelegateReference` and `SubscriptionToken` types.
