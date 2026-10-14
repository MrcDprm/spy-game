using SpyGame.Web.Models.Game;

namespace SpyGame.Web.Services.Storage;

/// <summary>
/// The same store kept in this process: for tests, and for running the site without Redis.
/// It stores JSON exactly like the Redis store, so a room that does not survive being written
/// and read back fails here too. Lifetimes are honoured with the given clock.
/// </summary>
public sealed class InMemoryGameStore(TimeProvider clock) : IRoomStore, ISessionStore, INicknameRegistry, ILeaderboardCache
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (string Value, DateTimeOffset Expires)> _values = [];
    private readonly Dictionary<string, DateTimeOffset> _deadlines = [];
    private readonly Dictionary<string, Dictionary<string, long>> _boards = [];

    private string? Read(string key)
    {
        if (!_values.TryGetValue(key, out var entry)) return null;
        if (entry.Expires > clock.GetUtcNow()) return entry.Value;
        _values.Remove(key);
        return null;
    }

    private void Write(string key, string value, TimeSpan lifetime) => _values[key] = (value, clock.GetUtcNow() + lifetime);

    public Task<bool> CreateAsync(Room room)
    {
        lock (_lock)
        {
            var key = StoreKeys.Room(room.Code);
            if (Read(key) is not null) return Task.FromResult(false);
            Write(key, GameJson.Write(room), StoreKeys.RoomLifetime);
            return Task.FromResult(true);
        }
    }

    public Task<Room?> GetAsync(string code)
    {
        lock (_lock) return Task.FromResult(Read(StoreKeys.Room(code)) is { } json ? GameJson.Read<Room>(json) : null);
    }

    public Task<RoomUpdate<T>> UpdateAsync<T>(string code, Func<Room, T> change)
    {
        lock (_lock)
        {
            var key = StoreKeys.Room(code);
            if (Read(key) is not { } json) return Task.FromResult(new RoomUpdate<T>(false, null, default));
            var room = GameJson.Read<Room>(json)!;
            var result = change(room);
            if (room.Players.Count == 0)
            {
                _values.Remove(key);
                _deadlines.Remove(code);
                return Task.FromResult(new RoomUpdate<T>(true, null, result));
            }
            Write(key, GameJson.Write(room), StoreKeys.RoomLifetime);
            return Task.FromResult(new RoomUpdate<T>(true, room, result));
        }
    }

    public Task SetDeadlineAsync(string code, DateTimeOffset? at)
    {
        lock (_lock)
        {
            if (at is { } time) _deadlines[code] = time;
            else _deadlines.Remove(code);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> DueAsync(DateTimeOffset now, int max = 100)
    {
        lock (_lock)
        {
            IReadOnlyList<string> due = _deadlines.Where(d => d.Value <= now).OrderBy(d => d.Value).Take(max).Select(d => d.Key).ToList();
            return Task.FromResult(due);
        }
    }

    Task<PlayerSession?> ISessionStore.GetAsync(string token)
    {
        lock (_lock) return Task.FromResult(Read(StoreKeys.Session(token)) is { } json ? GameJson.Read<PlayerSession>(json) : null);
    }

    public Task SaveAsync(string token, PlayerSession session)
    {
        lock (_lock) Write(StoreKeys.Session(token), GameJson.Write(session), StoreKeys.SessionLifetime);
        return Task.CompletedTask;
    }

    public Task<bool> TryReserveAsync(string nickname, string playerId)
    {
        lock (_lock)
        {
            var key = StoreKeys.Nickname(nickname);
            var owner = Read(key);
            if (owner is not null && owner != playerId) return Task.FromResult(false);
            Write(key, playerId, StoreKeys.NicknameLifetime);
            return Task.FromResult(true);
        }
    }

    public Task<bool> ExistsAsync(string week)
    {
        lock (_lock) return Task.FromResult(_boards.ContainsKey(week));
    }

    public Task AddAsync(string week, string accountId, long points)
    {
        lock (_lock)
        {
            var board = _boards.TryGetValue(week, out var b) ? b : _boards[week] = [];
            board[accountId] = board.GetValueOrDefault(accountId) + points;
        }
        return Task.CompletedTask;
    }

    public Task ReplaceAsync(string week, IReadOnlyCollection<WeeklyScore> scores)
    {
        lock (_lock)
        {
            if (scores.Count == 0) _boards.Remove(week);
            else _boards[week] = scores.ToDictionary(s => s.AccountId, s => s.Points);
        }
        return Task.CompletedTask;
    }

    // Same order as Redis: points high to low, ties by member (descending, like ZREVRANGE).
    private List<WeeklyScore> Ordered(string week) =>
        _boards.TryGetValue(week, out var board)
            ? board.OrderByDescending(e => e.Value).ThenByDescending(e => e.Key, StringComparer.Ordinal).Select(e => new WeeklyScore(e.Key, e.Value)).ToList()
            : [];

    public Task<IReadOnlyList<WeeklyScore>> TopAsync(string week, int count)
    {
        lock (_lock) return Task.FromResult<IReadOnlyList<WeeklyScore>>(Ordered(week).Take(count).ToList());
    }

    public Task<(long Rank, long Points)?> RankAsync(string week, string accountId)
    {
        lock (_lock)
        {
            var ordered = Ordered(week);
            var index = ordered.FindIndex(s => s.AccountId == accountId);
            return Task.FromResult<(long, long)?>(index < 0 ? null : (index, ordered[index].Points));
        }
    }

    public Task RemoveAsync(string week, string accountId)
    {
        lock (_lock) if (_boards.TryGetValue(week, out var board)) board.Remove(accountId);
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string nickname, string playerId)
    {
        lock (_lock)
        {
            var key = StoreKeys.Nickname(nickname);
            if (Read(key) == playerId) _values.Remove(key);
        }
        return Task.CompletedTask;
    }
}
