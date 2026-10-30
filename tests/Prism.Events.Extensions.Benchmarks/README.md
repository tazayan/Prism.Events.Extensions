# Benchmarks

These [BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks compare the extension's dispatch pipeline with Prism's `PubSubEvent`. Prism is the baseline; the `Ratio` column expresses how much faster or slower each method is for the same parameters. Async `Send` and `Publish` accept `ValueTask` subscribers, so their comparison with Prism measures dispatch cost rather than equivalent async completion semantics.

| Class | What it measures | Parameters |
|---|---|---|
| `PublishBenchmarks` | Publishing an `int` payload to publisher-thread subscribers: `PubSubEvent<int>.Publish` vs `LightweightPubSubEvent<int>.Publish`, `AsyncPubSubEvent<int>.Send`, and `AsyncPubSubEvent<int>.Publish` | 0, 1, 10, or 100 subscribers; strong or weak references |
| `ParameterlessPublishBenchmarks` | Publication without a payload | 1, 10, or 100 subscribers; strong or weak references |
| `FilteredPublishBenchmarks` | Subscribers with a filter, alternating accepted and rejected payloads | 10 or 100 subscribers; strong or weak references |
| `SubscriptionBenchmarks` | Subscribing a batch of callbacks and disposing their tokens | 10 or 100 subscribers |
| `ContendedPublishBenchmarks` | Four threads publishing to the same event at once | 10 subscribers |

Every benchmark reports allocated bytes (`MemoryDiagnoser`), plus lock contentions and thread-pool work items (`ThreadingDiagnoser`).

## Running

Run the benchmarks in Release from the repository root, explicitly selecting a target framework:

```powershell
# Reproduce the payload publication scenarios reported below.
dotnet run -c Release -f net10.0 --project tests/Prism.Events.Extensions.Benchmarks -- --filter '*Prism.Events.Extensions.Benchmarks.PublishBenchmarks.*'

# Everything (takes a while).
dotnet run -c Release -f net10.0 --project tests/Prism.Events.Extensions.Benchmarks -- --filter '*'

# Quick, less precise pass; not the DefaultJob used for the published results.
dotnet run -c Release -f net10.0 --project tests/Prism.Events.Extensions.Benchmarks -- --filter '*' --job short

# Compare all supported runtimes in the same run; each runtime must be installed.
dotnet run -c Release -f net10.0 --project tests/Prism.Events.Extensions.Benchmarks -- --filter '*Prism.Events.Extensions.Benchmarks.PublishBenchmarks.*' --runtimes net8.0 net9.0 net10.0
```

On Windows, add `--cpu-usage` to also collect a Visual Studio CPU usage trace for each benchmark.

### Finding the output

BenchmarkDotNet writes Markdown, HTML, and CSV reports to `BenchmarkDotNet.Artifacts/results`, relative to the process's working directory. Launching from the repository root normally creates that folder there; launching from `bin/Release/net10.0` creates it under that directory instead.

The supplied run's original files are under `tests/Prism.Events.Extensions.Benchmarks/bin/Release/net10.0/BenchmarkDotNet.Artifacts/results`. Build-output folders are Git-ignored. A documentation copy of the [complete Markdown report](results/2026-10-30-net10.0/PublishBenchmarks-report-github.md) is included here so the measurements and original statistics remain available on GitHub.

For stable measurements, close other applications, use a consistent CPU power configuration, and compare methods within the same run. Results from different machines or runtimes do not isolate the effect of a source change.

## Results: 2026-10-30, .NET 10

These results supersede the earlier indicative short-run Linux/.NET 8 figures. They come from the supplied `PublishBenchmarks` report, not a benchmark rerun performed while updating this documentation.

| Setting | Value |
|---|---|
| BenchmarkDotNet / job | 0.15.8 / `DefaultJob` |
| OS | Windows 11, 10.0.26300.9818 |
| CPU | Intel Core i9-11950H; 1 CPU, 8 physical / 16 logical cores; reported maximum 2.61 GHz |
| SDK | .NET SDK 10.0.403 |
| Runtime | .NET 10.0.14, X64 RyuJIT x86-64-v4 |
| Baseline dependency | Prism.Events 9.0.537 |
| Scenario | `int` payload; `ThreadOption.PublisherThread`; no custom filter |
| Callbacks | Non-allocating counter updates; async callbacks return `ValueTask.CompletedTask` |

All four methods use `int` payloads. Each cell below shows **mean time in nanoseconds / allocated bytes per publication**. The original report uses `-` for zero measured allocations; these tables display `0 B`. Zero subscribers are shown separately for each reference-strength parameter because they were measured as separate benchmark cases.

**Highlights:** 🟢 faster than Prism · 🔴 slower than Prism · 🔵 zero measured allocation. Multipliers are the reported BenchmarkDotNet ratios for the same subscriber count and reference strength.

Each result includes its faster/slower multiplier beneath the timing. For example, **3.43× faster** means Prism's measured dispatch time was about 3.43 times the extension's; it does not mean the entire application becomes 3.43 times faster.

### Strong references: `KeepSubscriberReferenceAlive = true`

| Subscribers | Prism `Publish` | Lightweight `Publish` | Async `Send` | Async `Publish` |
|---:|---:|---:|---:|---:|
| 0 | 21.694 ns / 88 B<br>Baseline | 2.573 ns / 🔵 **0 B**<br>🟢 **8.44× faster** | 12.187 ns / 🔵 **0 B**<br>🟢 **1.78× faster** | 17.163 ns / 🔵 **0 B**<br>🟢 **1.26× faster** |
| 1 | 44.278 ns / 248 B<br>Baseline | 27.900 ns / 🔵 **0 B**<br>🟢 **1.59× faster** | 42.487 ns / 🔵 **0 B**<br>🟢 **1.04× faster** | 48.558 ns / 🔵 **0 B**<br>🔴 **1.10× slower** |
| 10 | 220.992 ns / 1,424 B<br>Baseline | 65.615 ns / 🔵 **0 B**<br>🟢 **3.37× faster** | 129.572 ns / 🔵 **0 B**<br>🟢 **1.71× faster** | 135.876 ns / 🔵 **0 B**<br>🟢 **1.63× faster** |
| 100 | 1,768.971 ns / 12,648 B<br>Baseline | 515.504 ns / 🔵 **0 B**<br>🟢 **3.43× faster** | 1,066.591 ns / 🔵 **0 B**<br>🟢 **1.66× faster** | 1,080.997 ns / 🔵 **0 B**<br>🟢 **1.64× faster** |

### Weak references: `KeepSubscriberReferenceAlive = false`

| Subscribers | Prism `Publish` | Lightweight `Publish` | Async `Send` | Async `Publish` |
|---:|---:|---:|---:|---:|
| 0 | 22.551 ns / 88 B<br>Baseline | 2.610 ns / 🔵 **0 B**<br>🟢 **8.65× faster** | 12.200 ns / 🔵 **0 B**<br>🟢 **1.85× faster** | 17.465 ns / 🔵 **0 B**<br>🟢 **1.29× faster** |
| 1 | 253.575 ns / 312 B<br>Baseline | 240.374 ns / 64 B<br>🟢 **1.06× faster** | 318.501 ns / 64 B<br>🔴 **1.26× slower** | 330.009 ns / 64 B<br>🔴 **1.30× slower** |
| 10 | 2,225.064 ns / 2,064 B<br>Baseline | 2,027.077 ns / 640 B<br>🟢 **1.10× faster** | 2,428.060 ns / 640 B<br>🔴 **1.09× slower** | 2,403.451 ns / 640 B<br>🔴 **1.08× slower** |
| 100 | 21,496.678 ns / 19,048 B<br>Baseline | 19,020.167 ns / 6,400 B<br>🟢 **1.13× faster** | 23,197.027 ns / 6,400 B<br>🔴 **1.08× slower** | 23,529.524 ns / 6,400 B<br>🔴 **1.09× slower** |

### Interpretation

- **Strong subscribers:** lightweight publication is 1.59× faster than Prism with one subscriber, and 3.37× / 3.43× faster with 10 / 100 subscribers. It records 0 B allocated in every strong-reference case. Avoiding the strategy list, strategy delegates, and object-array payload transport accounts for the lower allocation footprint.
- **Weak subscribers:** lightweight publication is 1.06×, 1.10×, and 1.13× faster with 1, 10, and 100 subscribers. It allocates 64 B per subscriber in this benchmark: 64 B, 640 B, and 6,400 B versus Prism's 312 B, 2,064 B, and 19,048 B. This is approximately 79%, 69%, and 66% less allocated memory, respectively.
- **Liveness checks no longer reconstruct delegates:** subscriptions use `DelegateReference.TargetEquals(null)` to detect collected targets during pruning. Typed invocation then resolves each live callback once. This avoids the previous duplicate resolution in liveness checking and invocation; the weak callback still requires a delegate allocation when invoked. An explicitly weak filter would require its own resolution, which this no-custom-filter benchmark does not measure.
- **Async completion has its own overhead:** with 10 / 100 strong subscribers, `Send` is 1.71× / 1.66× faster than Prism and async `Publish` is 1.63× / 1.64× faster; both record 0 B allocated. With weak subscribers, `Send` is 1.26× / 1.09× / 1.08× slower for 1 / 10 / 100 subscribers, and async `Publish` is 1.30× / 1.08× / 1.09× slower, despite their lower allocations. For a single strong subscriber, `Send` is roughly comparable to Prism and async `Publish` is 1.10× slower.
- **Zero-subscriber results measure the empty path only:** all extension methods record 0 B, whereas Prism records 88 B. These timings do not describe subscriber execution.

Allocation-free results describe the warmed-up, publisher-thread cases with available pooled buffers and non-allocating handlers. They are not guarantees for cold pools, background/UI scheduling, weak references, custom filters, or callbacks that genuinely suspend. The async benchmark uses completed `ValueTask` callbacks; it does not measure I/O throughput, parallel handlers, or asynchronous scheduling.

The supplied folder contains results only for `PublishBenchmarks`. No new parameterless, filtered, contention, or subscription-churn measurements are included, so the older quantitative claims about those scenarios have been removed. Run the corresponding classes to measure them with the fixed implementation.

See the [complete BenchmarkDotNet report](results/2026-10-30-net10.0/PublishBenchmarks-report-github.md) for all 32 rows, errors, standard deviations, ratios, and GC statistics.
