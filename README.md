<p align="center">
  <img src="assets/logo.png" alt="Prism.Events.Extensions logo" width="200" />
</p>

# Prism.Events.Extensions

`Prism.Events.Extensions` extends `Prism.Events` with `ValueTask`-based asynchronous event subscriptions and a lower-allocation publish pipeline designed to improve runtime performance and memory efficiency.

This is an independent extension package and is not an official PrismLibrary package.

It adds two kinds of event to the Prism event aggregator:

- **`LightweightPubSubEvent` / `LightweightPubSubEvent<TPayload>`** can replace `PubSubEvent` and `PubSubEvent<TPayload>`. They have the same subscription API but allocate less memory on every publish. They don't create a list or a delegate per subscriber, and they don't box value-type payloads.
- **`AsyncPubSubEvent` / `AsyncPubSubEvent<TPayload>`** take subscribers that return a `ValueTask`. `Send` lets the publisher await every subscriber and observe their exceptions.

Both kinds derive from Prism's `EventBase`, so they work with `IEventAggregator.GetEvent<TEvent>()`.

## Install

```powershell
dotnet add package Async.Prism.Events.Extensions
```

## Usage

```csharp
using Prism.Events;
using Prism.Events.Extensions;

public sealed class OrderPlacedEvent : LightweightPubSubEvent<Order> { }
public sealed class OrderSavedEvent : AsyncPubSubEvent<Order> { }

// Synchronous, low-allocation event.
var placed = eventAggregator.GetEvent<OrderPlacedEvent>();
placed.Subscribe(order => Console.WriteLine(order.Id), ThreadOption.PublisherThread, false, order => order.Total > 0);
placed.Publish(order);

// Asynchronous event.
var saved = eventAggregator.GetEvent<OrderSavedEvent>();
saved.Subscribe(async order => await auditLog.WriteAsync(order), ThreadOption.BackgroundThread);
await saved.Send(order);
```

## Behavior

| | `PubSubEvent` (Prism) | `LightweightPubSubEvent` | `AsyncPubSubEvent` |
|---|---|---|---|
| Subscriber signature | `Action` / `Action<T>` | `Action` / `Action<T>` | `Func<ValueTask>` / `Func<T, ValueTask>` |
| Weak references, filters, `ThreadOption`, tokens | Yes | Yes | Yes |
| Per-publish allocations | A list plus one delegate per subscriber; value-type payloads are boxed | None for strongly referenced publisher-thread subscribers | None for strongly referenced publisher-thread subscribers that complete synchronously |
| Publisher can await subscribers | No | No | Yes, with `Send` |

- **Snapshots.** Each publication delivers to the live subscriptions that existed when it started. Subscriptions added or removed while it runs, including from inside a subscriber, take effect from the next publication. Subscribe, unsubscribe and publish are thread-safe.
- **Weak references.** By default subscribers are weakly referenced. A subscription is removed by the first publish after its target is garbage collected. Pass `keepSubscriberReferenceAlive: true` to hold a strong reference, then unsubscribe explicitly. Strong references are also much faster: Prism's `DelegateReference` creates a new delegate every time it resolves a weak reference, and that cost dominates publishing to weak subscribers (see the [benchmarks](https://github.com/tazayan/Prism.Events.Extensions/blob/main/tests/Prism.Events.Extensions.Benchmarks/README.md)).
- **Threads.** `PublisherThread` runs the subscriber synchronously. `BackgroundThread` uses the thread pool. `UIThread` posts to `EventBase.SynchronizationContext`. `EventAggregator` sets that context when it creates the event. If you create the event yourself, set it before you subscribe with `UIThread`.
- **`AsyncPubSubEvent.Send`** awaits subscribers one at a time, in subscription order, whatever their `ThreadOption`. If a subscriber throws, the returned task is faulted and the remaining subscribers are skipped.
- **`AsyncPubSubEvent.Publish`** is fire-and-forget (`async void`). An exception thrown by a subscriber is raised on the caller's `SynchronizationContext`, or on the thread pool if there is none. Prefer `Send`.

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

## Benchmarks

The benchmarks compare each event type with the Prism type it replaces. See [`tests/Prism.Events.Extensions.Benchmarks`](https://github.com/tazayan/Prism.Events.Extensions/blob/main/tests/Prism.Events.Extensions.Benchmarks/README.md) for how to run them.

## Create a NuGet package

```powershell
dotnet pack src/Prism.Events.Extensions/Prism.Events.Extensions.csproj --configuration Release
```

Packages are written to `artifacts/packages`. Release assemblies contain embedded portable PDBs with Source Link metadata, so supported IDEs can retrieve the exact matching source files from GitHub while debugging without a separate symbol package.

## License and attribution

Prism.Events.Extensions is licensed under the [MIT License](https://github.com/tazayan/Prism.Events.Extensions/blob/main/LICENSE).

Parts of the subscription types, resource strings and unit tests are adapted from Prism 8.1.97, which was released under the MIT License by the .NET Foundation. See [THIRD-PARTY-NOTICES.md](https://github.com/tazayan/Prism.Events.Extensions/blob/main/THIRD-PARTY-NOTICES.md).

At runtime this package depends on the [Prism.Events](https://www.nuget.org/packages/Prism.Events/) package. Prism 9 and later are distributed under the Prism Community License or the Prism Commercial License. Applications that use this package must comply with the license of the Prism.Events version they reference.
