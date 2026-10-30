using System.Runtime.CompilerServices;

namespace Prism.Events.Extensions.Benchmarks;

/// <summary>
/// A subscriber whose instance methods are used as event callbacks. Instance methods are used so
/// that weakly referenced subscriptions have a real target to resolve, as they do in applications.
/// </summary>
public sealed class Subscriber
{
    public int Count;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Handle() => Count++;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Handle(int payload) => Count += payload;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask HandleAsync()
    {
        Count++;
        return ValueTask.CompletedTask;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask HandleAsync(int payload)
    {
        Count += payload;
        return ValueTask.CompletedTask;
    }

    /// <summary>A filter that accepts even payloads.</summary>
    public bool AcceptEven(int payload) => (payload & 1) == 0;

    public static Subscriber[] CreateMany(int count)
    {
        var subscribers = new Subscriber[count];
        for (var i = 0; i < count; i++)
        {
            subscribers[i] = new Subscriber();
        }

        return subscribers;
    }
}
