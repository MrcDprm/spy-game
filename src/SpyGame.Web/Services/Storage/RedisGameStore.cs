using SpyGame.Web.Models.Game;
using StackExchange.Redis;

namespace SpyGame.Web.Services.Storage;

/// <summary>
/// Rooms, sessions and nicknames in Redis. Every "check, then write" happens inside one Lua
/// script, which Redis runs as a single step, so two requests can never both win a race.
/// </summary>
public sealed class RedisGameStore(IConnectionMultiplexer redis) : IRoomStore, ISessionStore, INicknameRegistry
{
    private const int MaxRetries = 20;
    private IDatabase Db => redis.GetDatabase();

    // Save only if nobody changed the room since we read it (compare-and-set).
    private const string CompareAndSet = """
        if redis.call('GET', KEYS[1]) ~= ARGV[1] then return 0 end
        if ARGV[2] == '' then redis.call('DEL', KEYS[1]) else redis.call('SET', KEYS[1], ARGV[2], 'EX', ARGV[3]) end
        return 1
        """;

    // Take the name if it is free or already ours, and keep it for another day.
    private const string Reserve = """
        local owner = redis.call('GET', KEYS[1])
        if owner == false or owner == ARGV[1] then
          redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[2])
          return 1
        end
        return 0
        """;

    private const string ReleaseIfOwner = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) end
        return 0
        """;

    public Task<bool> CreateAsync(Room room) =>
        Db.StringSetAsync(StoreKeys.Room(room.Code), GameJson.Write(room), StoreKeys.RoomLifetime, When.NotExists);

    public async Task<Room?> GetAsync(string code)
    {
        var json = await Db.StringGetAsync(StoreKeys.Room(code));
        return json.IsNullOrEmpty ? null : GameJson.Read<Room>(json.ToString());
    }

    public async Task<RoomUpdate<T>> UpdateAsync<T>(string code, Func<Room, T> change)
    {
        var key = StoreKeys.Room(code);
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var before = await Db.StringGetAsync(key);
            if (before.IsNullOrEmpty) return new RoomUpdate<T>(false, null, default);

            var room = GameJson.Read<Room>(before.ToString())!;
            var result = change(room);
            var after = room.Players.Count == 0 ? "" : GameJson.Write(room);

            var saved = (int)await Db.ScriptEvaluateAsync(CompareAndSet, [key],
                [before, after, (long)StoreKeys.RoomLifetime.TotalSeconds]);
            if (saved == 1)
            {
                if (after == "") await SetDeadlineAsync(code, null);
                return new RoomUpdate<T>(true, after == "" ? null : room, result);
            }
        }
        throw new InvalidOperationException($"Room {code} is too busy to update.");
    }

    public Task SetDeadlineAsync(string code, DateTimeOffset? at) => at is { } time
        ? Db.SortedSetAddAsync(StoreKeys.Deadlines, code, time.ToUnixTimeMilliseconds())
        : Db.SortedSetRemoveAsync(StoreKeys.Deadlines, code);

    public async Task<IReadOnlyList<string>> DueAsync(DateTimeOffset now, int max = 100)
    {
        var due = await Db.SortedSetRangeByScoreAsync(StoreKeys.Deadlines, double.NegativeInfinity, now.ToUnixTimeMilliseconds(), take: max);
        return due.Select(v => v.ToString()).ToList();
    }

    // Both interfaces have a GetAsync(string), so this one is reached through ISessionStore.
    async Task<PlayerSession?> ISessionStore.GetAsync(string token)
    {
        var json = await Db.StringGetAsync(StoreKeys.Session(token));
        return json.IsNullOrEmpty ? null : GameJson.Read<PlayerSession>(json.ToString());
    }

    public Task SaveAsync(string token, PlayerSession session) =>
        Db.StringSetAsync(StoreKeys.Session(token), GameJson.Write(session), StoreKeys.SessionLifetime);

    public async Task<bool> TryReserveAsync(string nickname, string playerId) =>
        (int)await Db.ScriptEvaluateAsync(Reserve, [StoreKeys.Nickname(nickname)],
            [playerId, (long)StoreKeys.NicknameLifetime.TotalSeconds]) == 1;

    public Task ReleaseAsync(string nickname, string playerId) =>
        Db.ScriptEvaluateAsync(ReleaseIfOwner, [StoreKeys.Nickname(nickname)], [playerId]);
}
