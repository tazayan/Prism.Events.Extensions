# <img src="assets/logo.png" alt="" width="70" height="70" align="middle" /> Prism.Events.Extensions

`Prism.Events.Extensions` adds high-performance synchronous events and awaitable asynchronous events to the [Prism event aggregator](https://github.com/PrismLibrary/Prism). It keeps Prism's event-based programming model while reducing the dispatch overhead and temporary allocations associated with `PubSubEvent`.

This is an independent extension package and is not an official PrismLibrary package.

## NuGet Packages

Install the latest stable release from [NuGet.org](https://www.nuget.org/packages/Async.Prism.Events.Extensions).

| Platform | Package | NuGet |
|---|---|---|---|
| Cross-platform (.NET 8, 9, and 10) | [Async.Prism.Events.Extensions](https://www.nuget.org/packages/Async.Prism.Events.Extensions) | [![NuGet version](https://img.shields.io/nuget/v/Async.Prism.Events.Extensions.svg)](https://www.nuget.org/packages/Async.Prism.Events.Extensions) |

### Install

```powershell
dotnet add package Async.Prism.Events.Extensions
```

## Purpose

The library addresses two separate needs:

1. **Lower-overhead publication.** `LightweightPubSubEvent` and `LightweightPubSubEvent<TPayload>` are alternatives to Prism's `PubSubEvent` types for frequently published events. Their normal publish path avoids per-subscriber execution-strategy delegates, payload boxing, and a newly allocated strategy list on every publication. This reduces time spent dispatching events and the amount of short-lived garbage the GC must collect.
2. **Asynchronous subscribers with observable completion.** `AsyncPubSubEvent` and `AsyncPubSubEvent<TPayload>` accept callbacks returning `ValueTask`. Publishers can use `Send` to await subscribers and observe their exceptions, or `Publish` for fire-and-forget delivery. These events share the typed, pooled dispatch design; their main purpose is async support, not to outperform the synchronous lightweight events.

All four types derive from Prism's `EventBase` and work with `IEventAggregator.GetEvent<TEvent>()`. They retain thread options, weak or strong references, subscription tokens, and payload filters.

| Event type | Subscriber | Publication | Use when |
|---|---|---|---|
| `LightweightPubSubEvent` | `Action` | `Publish()` | A notification needs no payload. |
| `LightweightPubSubEvent<TPayload>` | `Action<TPayload>` | `Publish(payload)` | A notification carries a strongly typed payload. |
| `AsyncPubSubEvent` | `Func<ValueTask>` | `await Send()` or `Publish()` | Subscribers do asynchronous work without a payload. |
| `AsyncPubSubEvent<TPayload>` | `Func<TPayload, ValueTask>` | `await Send(payload)` or `Publish(payload)` | Subscribers do asynchronous work with a typed payload. |

## Usage

```csharp
using Prism.Events;
using Prism.Events.Extensions;

public sealed class OrderPlacedEvent : LightweightPubSubEvent<Order> { }
public sealed class OrderSavedEvent : AsyncPubSubEvent<Order> { }
```

Subscribe and publish through your existing `IEventAggregator`:

```csharp
// Keep these tokens for the subscriber's lifetime; dispose them when it is done.
// Strong references are used here to demonstrate the lowest-allocation path.
var placed = eventAggregator.GetEvent<OrderPlacedEvent>();
var placedSubscription = placed.Subscribe(
    payload => Console.WriteLine(payload.Id),
    ThreadOption.PublisherThread,
    keepSubscriberReferenceAlive: true,
    filter: payload => payload.Total > 0);

// Synchronous, low-allocation event.
placed.Publish(order);

var saved = eventAggregator.GetEvent<OrderSavedEvent>();
var savedSubscription = saved.Subscribe(
    async payload => await auditLog.WriteAsync(payload),
    ThreadOption.PublisherThread,
    keepSubscriberReferenceAlive: true);

// Await subscribers, including their asynchronous work, and observe exceptions.
await saved.Send(order);

// When the subscriber is disposed or no longer needs the events:
placedSubscription.Dispose();
savedSubscription.Dispose();
```

`PublisherThread` does not force an asynchronous callback to finish synchronously: it starts the callback on the publishing thread, and `Send` awaits its returned `ValueTask`. Use `BackgroundThread` to schedule the callback on the thread pool, or `UIThread` to dispatch through the event's synchronization context.

For synchronous events, migrating usually means changing an event declaration from `PubSubEvent<TPayload>` to `LightweightPubSubEvent<TPayload>` (or the corresponding parameterless type), while retaining the usual subscription and publication calls. The new event types derive directly from `EventBase`, not from `PubSubEvent`; code that explicitly requires or casts to a `PubSubEvent` type must be updated. Also review the delivery and threading behavior below.

## Why publication is faster and more memory-efficient

The comparison here is with the `PubSubEvent` implementation used by the repository's Prism.Events 9.0.537 dependency. The savings come from changing the dispatch pipeline, not from reducing the work performed inside your subscribers.

### 1. Separate, strongly typed invocation paths

Prism's generic and parameterless subscriptions both expose their publication strategies as `Action<object[]>` through `IEventSubscription.GetExecutionStrategy()`. The extension provides separate implementations for the two event shapes, invoking `InvokeAction()` or `InvokeAction(TPayload)` directly. Async subscriptions similarly use methods returning `ValueTask`.

The shared object-array interface is still implemented for compatibility, but the normal `Publish` and `Send` paths do not use `GetExecutionStrategy()`. Separate implementations allow each event shape to keep its own typed invocation path while remaining compatible with Prism's interfaces.

### 2. No execution-strategy delegate per subscriber per publication

Prism's `GetExecutionStrategy()` creates a capturing lambda for each live subscription on each publication. That strategy holds the callback (and filter, when present) and later invokes it. The resulting delegates and closure objects become short-lived allocations.

The extension calls each subscription's typed invocation method instead. It does not create these execution-strategy wrappers on the normal publish path. This removes allocation and dispatch overhead that otherwise grows with the subscriber count. Your own callbacks can, of course, still allocate.

### 3. No object-array payload transport or value-type boxing

Prism's `PubSubEvent<TPayload>.Publish(payload)` forwards the payload into `EventBase.InternalPublish(params object[] arguments)`. A payload-bearing publish creates an object array; a value-type payload such as an `int`, `enum`, or `struct` is boxed when stored in it, and each execution strategy extracts the typed argument again.

`LightweightPubSubEvent<TPayload>.Publish(payload)` and `AsyncPubSubEvent<TPayload>.Send(payload)` / `Publish(payload)` keep the payload as `TPayload` throughout normal dispatch. They do not create that argument array or box the payload to pass it to callbacks and filters. The parameterless path needs no payload container. These claims apply to the public typed paths, not to callers that explicitly use the object-array compatibility interfaces.

### 4. Pooled subscriber snapshots instead of a new strategy list

Prism's `EventBase.InternalPublish` calls `PruneAndReturnStrategies`, which creates a new `List<Action<object[]>>` on every publication, with backing-array allocations as strategies are added.

The extension prunes dead subscriptions and copies the remaining subscription references into an array rented from `ArrayPool<IEventSubscription>.Shared`. It invokes callbacks outside the subscription lock and returns the array in a `finally` block, clearing its references so the pool does not retain subscriber objects. The same snapshot mechanism is used by both lightweight and async events.

After warm-up, available pooled buffers can be reused without a new collection allocation for each publication. A cold or depleted pool can still allocate. There is still a snapshot copy and linear work in the number of subscribers: pooling reuses storage; it does not eliminate storage, copying, or synchronization.

For implementation details, see the [shared snapshot helper](https://github.com/tazayan/Prism.Events.Extensions/blob/main/src/Prism.Events.Extensions/ParameterlessEvents/RegularEvents/LightweightPubSubEvent.cs), [typed synchronous subscription](https://github.com/tazayan/Prism.Events.Extensions/blob/main/src/Prism.Events.Extensions/PayloadEvents/RegularEvents/EventSubscription.cs), and [async publication loop](https://github.com/tazayan/Prism.Events.Extensions/blob/main/src/Prism.Events.Extensions/PayloadEvents/TaskEvents/AsyncPubSubEvent.cs).

### Allocation boundaries and trade-offs

**Zero-allocation publication is a conditional steady-state result, not a guarantee for every configuration.** It applies to the measured publisher-thread path with strongly referenced subscribers, available pooled buffers, and non-allocating callbacks and filters. Async callbacks must also complete synchronously for the measured zero-allocation async path.

- **Weak references are the default.** The extension reuses Prism's `DelegateReference`. Liveness checks use `TargetEquals(null)` without constructing delegates, so pruning does not duplicate weak-delegate resolution. Invoking a live weak callback still reconstructs its delegate once; an explicitly weak filter also needs resolution. Avoiding strategy wrappers does not remove those invocation allocations, so weak- and strong-reference workloads have different timing and allocation results.
- **Strong references need lifetime management.** `keepSubscriberReferenceAlive: true` avoids weak-delegate reconstruction but keeps callback and filter targets alive until unsubscribed. Dispose the subscription token or explicitly unsubscribe when the subscriber is done.
- **Background and UI dispatch have their own costs.** Scheduling can allocate tasks, closures, or synchronization-context work items. The zero-allocation publisher-thread measurements do not apply to these paths.
- **Async suspension has a cost.** `ValueTask` allows a synchronously completed path without a `Task` allocation for every handler, but genuinely asynchronous work, its state machines, scheduling, and subscriber code can still allocate.
- **Subscriptions are not free.** Creating events, delegates, subscriptions, and tokens allocates. The optimization targets repeated `Publish` / `Send` calls, not allocation-free setup.

## Benchmarks

The benchmark suite compares the extension with Prism using the same subscribers, payloads, reference strengths, and thread options. It includes payload and parameterless publication, filters, subscription churn, and concurrent publishers, with allocation measurements as well as timing.

The following results are from the supplied **2026-10-30 .NET 10 run**: BenchmarkDotNet 0.15.8 `DefaultJob`, Windows 11, Intel Core i9-11950H, .NET SDK 10.0.403, and .NET runtime 10.0.14. The baseline is Prism.Events 9.0.537. This run measures `int` payloads on `ThreadOption.PublisherThread`, with non-allocating callbacks and synchronously completed `ValueTask` handlers. Each cell shows **mean time / allocated bytes per publication**.

**Highlights:** 🟢 faster than Prism · 🔴 slower than Prism · 🔵 zero measured allocation. Multipliers are the reported BenchmarkDotNet ratios for the same subscriber count and reference strength.

| Subscribers | Prism `Publish` | Lightweight `Publish` | Async `Send` |
|---:|---|---:|---:|
| 10  | 220.992 ns / 1,424 B<br>Baseline | 65.615 ns / 🔵 **0 B**<br>🟢 **3.37× faster** | 129.572 ns / 🔵 **0 B**<br>🟢 **1.71× faster** |
| 100 | 1,768.971 ns / 12,648 B<br>Baseline | 515.504 ns / 🔵 **0 B**<br>🟢 **3.43× faster** | 1,066.591 ns / 🔵 **0 B**<br>🟢 **1.66× faster** |

In this run:

- 🟢 With **10 and 100 strong subscribers**, lightweight publication was **3.37× and 3.43× faster** than Prism and allocated **0 B**, versus Prism's 1,424 B and 12,648 B per publication.
- Async `Send` was **1.71× and 1.66× faster** than the synchronous Prism baseline with 10 and 100 strong subscribers. It allocated the same bytes as the lightweight event in these scenarios. Async support provides awaitable completion, not a guarantee of faster dispatch in every configuration.

The full report also includes zero/one-subscriber cases and async `Publish`, including small workloads where async dispatch is slower than Prism. See the [benchmark guide, full tables, and interpretation](https://github.com/tazayan/Prism.Events.Extensions/blob/main/tests/Prism.Events.Extensions.Benchmarks/README.md) and the [original BenchmarkDotNet report](https://github.com/tazayan/Prism.Events.Extensions/blob/main/tests/Prism.Events.Extensions.Benchmarks/results/2026-10-30-net10.0/PublishBenchmarks-report-github.md) for all 32 measurements, uncertainty, and allocation data.

These results replace the older indicative Linux/.NET 8 figures. They describe this hardware, runtime, and scenario; they are not performance guarantees or measurements of asynchronous I/O, background/UI dispatch, filters, contention, or subscription churn. Prism's baseline does not provide an equivalent awaitable async publication API. Reproduce measurements on your own hardware before drawing conclusions about your workload.

The [allocation regression tests](https://github.com/tazayan/Prism.Events.Extensions/blob/main/tests/Prism.Events.Extensions/Events/LightweightPubSubEventExtensionFixture.cs) also check that a warmed-up, strongly referenced publisher-thread `Publish` does not allocate a snapshot collection or box an `int` payload.

## Delivery and async semantics

| | `PubSubEvent` (Prism) | `LightweightPubSubEvent` | `AsyncPubSubEvent` |
|---|---|---|---|
| Subscriber signature | `Action` / `Action<T>` | `Action` / `Action<T>` | `Func<ValueTask>` / `Func<T, ValueTask>` |
| Weak references, filters, `ThreadOption`, tokens | Yes | Yes | Yes |
| Dispatch design | Object-array strategies and a new strategy list | Typed invocation and pooled snapshot | Typed, awaitable invocation and pooled snapshot |
| Publisher can await subscribers | No | No | Yes, with `Send` |

- **Snapshots.** Each publication uses a stable snapshot of the subscriptions observed when it starts. Subscriptions added or removed while it runs, including from inside a subscriber, affect subsequent snapshots; unsubscription does not cancel an in-flight delivery. Weakly referenced callbacks can still disappear before invocation. Subscribe, unsubscribe, and publish operations synchronize access to the subscription list; concurrent publications can invoke your handlers concurrently, so handler state must be protected separately.
- **Threads and filters.** `PublisherThread` invokes the callback on the publishing thread; async continuations follow normal async/context behavior. `BackgroundThread` uses the thread pool. `UIThread` dispatches through `EventBase.SynchronizationContext`. `EventAggregator` sets that context when it creates the event; if you create the event yourself, set it before subscribing with `UIThread`. Payload filters run on the subscription's selected dispatch thread, unlike Prism's strategy path, which evaluates filters on the publishing thread.
- **`AsyncPubSubEvent.Send`** awaits subscribers one at a time, in subscription order, whatever their `ThreadOption`. If a subscriber throws, the returned `ValueTask` faults and the remaining subscribers are skipped.
- **`AsyncPubSubEvent.Publish`** is fire-and-forget (`async void`). An exception thrown by a subscriber is raised on the caller's `SynchronizationContext`, or on the thread pool if there is none. Prefer `Send`.
- **Completion is not a delivery guarantee.** `Send` completes immediately when there are no subscribers; filtered-out or collected weak subscribers do no work. It provides neither a result nor a guarantee that a particular business operation happened. Use an explicit service dependency or request/response contract when a required handler or result is part of your application's contract.

Passing an `async` lambda to Prism's `PubSubEvent<TPayload>.Subscribe(Action<TPayload>)` creates an `async void` handler: the publisher cannot await it or observe its asynchronous exceptions through `Publish`. Subscribing to `AsyncPubSubEvent<TPayload>` instead creates a `Func<TPayload, ValueTask>`, giving `Send` a completion and exception path to await.

## Build

The repository pins its .NET SDK in `global.json`, targets .NET 8, .NET 9, and .NET 10 from `Directory.Build.props`, and manages dependency versions centrally in `Directory.Packages.props`.

```powershell
dotnet restore Prism.Events.Extensions.slnx
dotnet build Prism.Events.Extensions.slnx --configuration Release --no-restore

$frameworks = dotnet msbuild tests/Prism.Events.Extensions/Prism.Events.Extensions.Tests.csproj -nologo -getProperty:TargetFrameworks
$frameworks.Split(';') | ForEach-Object {
    dotnet test --project tests/Prism.Events.Extensions/Prism.Events.Extensions.Tests.csproj --configuration Release --framework $_ --no-build
}
```

The tests use [xUnit.net v3](https://xunit.net/) on [Microsoft Testing Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro) v2. `global.json` selects the Microsoft Testing Platform runner for `dotnet test`, which requires the .NET 10 SDK. The test project is also an executable, so you can run it directly, for example `dotnet run --project tests/Prism.Events.Extensions -f net10.0 -c Release`.

To collect code coverage in Cobertura format:

```powershell
dotnet test --project tests/Prism.Events.Extensions/Prism.Events.Extensions.Tests.csproj --configuration Release --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --coverage-settings tests/Prism.Events.Extensions/coverage.settings.xml
```

## Create a NuGet package

```powershell
dotnet pack src/Prism.Events.Extensions/Prism.Events.Extensions.csproj --configuration Release
```

Packages are written to `artifacts/packages`. Release assemblies contain embedded portable PDBs with Source Link metadata, so supported IDEs can retrieve the exact matching source files from GitHub while debugging without a separate symbol package.

## License and attribution

Prism.Events.Extensions is licensed under the [MIT License](https://github.com/tazayan/Prism.Events.Extensions/blob/main/LICENSE).

Parts of the subscription types, resource strings and unit tests are adapted from Prism 8.1.97, which was released under the MIT License by the .NET Foundation. See [THIRD-PARTY-NOTICES.md](https://github.com/tazayan/Prism.Events.Extensions/blob/main/THIRD-PARTY-NOTICES.md).

At runtime this package depends on the [Prism.Events](https://www.nuget.org/packages/Prism.Events/) package. Prism 9 and later are distributed under the Prism Community License or the Prism Commercial License. Applications that use this package must comply with the license of the Prism.Events version they reference.
