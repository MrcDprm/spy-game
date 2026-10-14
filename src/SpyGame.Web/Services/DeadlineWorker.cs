using SpyGame.Web.Services.Storage;

namespace SpyGame.Web.Services;

/// <summary>
/// The game clock. Twice a second it asks the store which rooms are past their deadline
/// (one sorted-set query in Redis) and moves them on: questions → voting → results.
/// </summary>
public sealed class DeadlineWorker(IRoomStore store, RoomService rooms, TimeProvider clock, ILogger<DeadlineWorker> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var code in await store.DueAsync(clock.GetUtcNow())) await rooms.TickAsync(code);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // A Redis hiccup must not stop the clock; the rooms are still due next time.
                log.LogWarning(e, "Deadline tick failed");
            }
        }
    }
}
