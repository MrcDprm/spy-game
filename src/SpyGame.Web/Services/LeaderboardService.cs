using Microsoft.EntityFrameworkCore;
using SpyGame.Web.Data;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Web.Services;

/// <summary>One line of the leaderboard. Equal points share a rank (1, 2, 2, 4).</summary>
public sealed record LeaderboardLine(int Rank, string AccountId, string Nickname, long Points);

/// <summary>
/// "Spies of the week". Every finished round adds the points of confirmed accounts to the
/// database (the history) and to a Redis sorted set (fast reads). If the sorted set is missing,
/// it is rebuilt from the database first, so no points are ever lost to a Redis restart.
/// Banned accounts are left out everywhere.
/// </summary>
public sealed class LeaderboardService(IServiceScopeFactory scopes, ILeaderboardCache cache, TimeProvider clock) : IRoundRecorder
{
    public string CurrentWeek => Week.Of(clock.GetUtcNow());

    public async Task RecordAsync(Room room)
    {
        var round = room.Round!;
        var won = room.Players
            .Where(p => p.AccountId is not null && round.Points!.GetValueOrDefault(p.Id) > 0)
            .ToDictionary(p => p.AccountId!, p => round.Points![p.Id]);
        if (won.Count == 0) return;

        var week = Week.Of(clock.GetUtcNow());
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eligible = await db.Users
            .Where(u => won.Keys.Contains(u.Id) && u.EmailConfirmed && !u.LeaderboardBanned)
            .Select(u => u.Id)
            .ToListAsync();
        if (eligible.Count == 0) return;

        foreach (var id in eligible)
            db.Scores.Add(new ScoreEntry { AccountId = id, Week = week, Points = won[id], RoomCode = room.Code, Round = round.Number, At = clock.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return; // this round was already recorded (unique account + room + round + week)
        }

        if (!await cache.ExistsAsync(week)) await RebuildAsync(db, week); // includes the rows just saved
        else foreach (var id in eligible) await cache.AddAsync(week, id, won[id]);
    }

    public async Task<IReadOnlyList<LeaderboardLine>> TopAsync(string week, int count)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await cache.ExistsAsync(week)) await RebuildAsync(db, week);

        var top = await cache.TopAsync(week, count);
        var ids = top.Select(s => s.AccountId).ToList();
        var names = await db.Users.Where(u => ids.Contains(u.Id) && !u.LeaderboardBanned).ToDictionaryAsync(u => u.Id, u => u.UserName!);

        var lines = new List<LeaderboardLine>();
        var rank = 0;
        long? previous = null;
        foreach (var (score, position) in top.Where(s => names.ContainsKey(s.AccountId)).Select((s, i) => (s, i)))
        {
            if (score.Points != previous) rank = position + 1;
            previous = score.Points;
            lines.Add(new LeaderboardLine(rank, score.AccountId, names[score.AccountId], score.Points));
        }
        return lines;
    }

    /// <summary>This account's position (1-based, ties not merged) and points, or null if it has none this week.</summary>
    public async Task<(long Position, long Points)?> PositionAsync(string week, string accountId)
    {
        if (!await cache.ExistsAsync(week))
        {
            using var scope = scopes.CreateScope();
            await RebuildAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), week);
        }
        return await cache.RankAsync(week, accountId) is { } r ? (r.Rank + 1, r.Points) : null;
    }

    // --- admin ---

    /// <summary>Deletes an account's points for one week.</summary>
    public async Task RemoveWeekAsync(string accountId, string week)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Scores.Where(s => s.AccountId == accountId && s.Week == week).ExecuteDeleteAsync();
        await cache.RemoveAsync(week, accountId);
    }

    /// <summary>Keeps (or lets back) an account off the leaderboard. Its history stays in the database.</summary>
    public async Task SetBannedAsync(string accountId, bool banned)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Users.Where(u => u.Id == accountId).ExecuteUpdateAsync(u => u.SetProperty(x => x.LeaderboardBanned, banned));
        foreach (var week in new[] { CurrentWeek, Week.Previous(CurrentWeek) }) await RebuildAsync(db, week);
    }

    private async Task RebuildAsync(AppDbContext db, string week)
    {
        var totals = await db.Scores
            .Where(s => s.Week == week && !s.Account!.LeaderboardBanned)
            .GroupBy(s => s.AccountId)
            .Select(g => new WeeklyScore(g.Key, g.Sum(s => (long)s.Points)))
            .ToListAsync();
        await cache.ReplaceAsync(week, totals);
    }
}
