using SpyGame.Web.Models.Game;

namespace SpyGame.Web.Services.Storage;

/// <summary>The result of a change to a room: whether it existed, the room afterwards, and what the change returned.</summary>
public sealed record RoomUpdate<T>(bool Found, Room? Room, T? Result);

/// <summary>Live rooms. Kept in Redis in production so a restart does not end the games.</summary>
public interface IRoomStore
{
    /// <summary>Saves a new room; false if a room with that code already exists.</summary>
    Task<bool> CreateAsync(Room room);

    Task<Room?> GetAsync(string code);

    /// <summary>
    /// Reads the room, applies <paramref name="change"/> and saves it, retrying if someone else
    /// saved the room in between (two players voting at the same moment). A room left with no
    /// players is deleted.
    /// </summary>
    Task<RoomUpdate<T>> UpdateAsync<T>(string code, Func<Room, T> change);

    /// <summary>When the room's current phase ends (null: no deadline).</summary>
    Task SetDeadlineAsync(string code, DateTimeOffset? at);

    /// <summary>Rooms whose deadline has passed.</summary>
    Task<IReadOnlyList<string>> DueAsync(DateTimeOffset now, int max = 100);
}

/// <summary>A browser's player: a random id, the nickname they chose, and their account once they log in.</summary>
public sealed record PlayerSession(string PlayerId, string? Nickname, string? AccountId = null);

/// <summary>Guest sessions, found by the (hashed) token in the browser's cookie.</summary>
public interface ISessionStore
{
    Task<PlayerSession?> GetAsync(string token);

    /// <summary>Saves the session and keeps it for another 24 hours.</summary>
    Task SaveAsync(string token, PlayerSession session);
}

/// <summary>Nicknames in use. A name belongs to one player until they have been away for 24 hours.</summary>
public interface INicknameRegistry
{
    /// <summary>True if the name was free or already this player's; the reservation is renewed.</summary>
    Task<bool> TryReserveAsync(string nickname, string playerId);

    /// <summary>Frees the name, but only if it belongs to this player.</summary>
    Task ReleaseAsync(string nickname, string playerId);
}

/// <summary>One account's total for a week.</summary>
public sealed record WeeklyScore(string AccountId, long Points);

/// <summary>
/// The weekly leaderboard as a Redis sorted set (account id → points): adding, the top of the
/// list and one player's rank are each a single command. The database keeps the history it is
/// rebuilt from.
/// </summary>
public interface ILeaderboardCache
{
    Task<bool> ExistsAsync(string week);
    Task AddAsync(string week, string accountId, long points);
    Task ReplaceAsync(string week, IReadOnlyCollection<WeeklyScore> scores);
    Task<IReadOnlyList<WeeklyScore>> TopAsync(string week, int count);
    /// <summary>0-based position (highest points first) and points, or null if not on the board.</summary>
    Task<(long Rank, long Points)?> RankAsync(string week, string accountId);
    Task RemoveAsync(string week, string accountId);
}

/// <summary>A simple counter per key and time window, for limits that are not per request.</summary>
public interface IRateCounter
{
    /// <summary>Counts one more use; false once <paramref name="limit"/> uses happened within the window.</summary>
    Task<bool> TryHitAsync(string key, int limit, TimeSpan window);
}

internal static class StoreKeys
{
    public static readonly TimeSpan RoomLifetime = TimeSpan.FromHours(3);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan NicknameLifetime = TimeSpan.FromHours(24);

    public const string Deadlines = "sg:deadlines";
    public static readonly TimeSpan LeaderboardLifetime = TimeSpan.FromDays(21); // this week and the last, with room to spare
    public static string Leaderboard(string week) => $"sg:lb:{week}";
    // "room2": rooms saved before places replaced categories are left to expire, never read.
    public static string Room(string code) => $"sg:room2:{code}";
    public static string Session(string token) => $"sg:session:{Hash(token)}";
    public static string Nickname(string name) => $"sg:nick:{name}";
    public static string Counter(string key) => $"sg:count:{Hash(key)}";

    // Only a hash of the cookie is stored: someone who reads Redis cannot take over a player.
    private static string Hash(string token) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
