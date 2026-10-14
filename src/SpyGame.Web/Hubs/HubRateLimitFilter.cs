using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace SpyGame.Web.Hubs;

/// <summary>
/// A connection may call the hub at most 20 times in 10 seconds; more is refused with
/// "rate_limited" without reaching the game. Plenty for a person, not for a script.
/// </summary>
public sealed class HubRateLimitFilter(TimeProvider clock) : IHubFilter
{
    public const int Limit = 20;
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _calls = new();

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var calls = _calls.GetOrAdd(context.Context.ConnectionId, _ => new Queue<DateTimeOffset>());
        var now = clock.GetUtcNow();
        lock (calls)
        {
            while (calls.Count > 0 && now - calls.Peek() >= Window) calls.Dequeue();
            if (calls.Count >= Limit) return "rate_limited";
            calls.Enqueue(now);
        }
        return await next(context);
    }

    public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        _calls.TryRemove(context.Context.ConnectionId, out _);
        return next(context, exception);
    }
}
