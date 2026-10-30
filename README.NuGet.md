# Prism.Events.Extensions

`Prism.Events.Extensions` adds high-performance synchronous events and awaitable asynchronous events to the [Prism event aggregator](https://github.com/PrismLibrary/Prism). It keeps Prism's event-based programming model while reducing the dispatch overhead and temporary allocations associated with `PubSubEvent`.

This is an independent extension package and is not an official PrismLibrary package.

## Purpose

The library addresses two separate needs:

1. **Lower-overhead publication.** `LightweightPubSubEvent` and `LightweightPubSubEvent<TPayload>` are alternatives to Prism's `PubSubEvent` types for frequently published events. Their normal publish path avoids per-subscriber execution-strategy delegates, payload boxing, and a newly allocated strategy list on every publication. This reduces time spent dispatching events and the amount of short-lived garbage the GC must collect.
2. **Asynchronous subscribers with observable completion.** `AsyncPubSubEvent` and `AsyncPubSubEvent<TPayload>` accept callbacks returning `ValueTask`. Publishers can use `Send` to await subscribers and observe their exceptions, or `Publish` for fire-and-forget delivery. These events share the typed, pooled dispatch design; their main purpose is async support, not to outperform the synchronous lightweight events.

## Install

```shell
dotnet add package Async.Prism.Events.Extensions
```

## Documentation

See the [repository README](https://github.com/tazayan/Prism.Events.Extensions/blob/main/README.md) for usage examples, performance details, benchmarks, and delivery semantics.
